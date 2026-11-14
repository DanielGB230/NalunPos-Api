# ADR 0025: Separación de Permisos de Plataforma, Catálogo Canónico SuperAdmin y Tipado de Estado de Tenants en el Contrato

- **Estado:** Aceptado
- **Fecha:** 2026-11-14
- **Contexto:**
  1. En una arquitectura SaaS multi-tenant con separación estricta entre el plano de control (Control Plane / Platform) y el plano de aplicación (Application Plane / Tenants), los privilegios operativos de plataforma jamás deben ser delegables ni asignables a roles de tenant.
  2. Históricamente, `DefaultTenantRoleCatalog` contenía permisos como `Permissions.Tenants.View` y `Permissions.Tenants.Update` en el rol de empresa `TenantAdmin`. Esto violaba el principio de aislamiento multi-tenant al mezclar capacidades globales de gestión de tenants dentro del catálogo de roles internos de las empresas cliente.
  3. Durante la verificación en vivo contra SQL Server, se constató que el rol `SuperAdmin` en la base de datos únicamente poseía los permisos `Permissions.Platform.DeadLetter.View` y `Permissions.Platform.DeadLetter.Reprocess`. Al requerir los endpoints `/api/v1/platform/tenants` los permisos `Permissions.Tenants.View` y `Permissions.Tenants.Create`, el usuario `SuperAdmin` legítimo recibía un código `403 Forbidden` al consultar o crear tenants.
  4. Los tests de integración previos ocultaban esta brecha porque agregaban permisos manualmente en los fixtures (`MsSqlTestFixture`) o en tests aislados (`AuthLoginSqlServerIntegrationTests`), en lugar de evaluar el comportamiento del `SuperAdminSeeder` y del rol sembrado real tal como opera en un entorno productivo.
  5. Adicionalmente, el DTO `TenantDto.Status` exponía el estado como un `string` libre sin tipado estricto en el contrato OpenAPI, lo que provocó desacoplamientos silenciosos en el cliente frontend (por ejemplo, asumiendo valores como `"Pending"` en lugar del enum de dominio `TenantStatus.PendingProvisioning`).
  6. Finalmente, en el paso F1.A.1 se identificó que la verificación de no-exposición de endpoints OpenAPI y Scalar fuera de `Development` requería un aserto estricto de `404 Not Found` bajo peticiones autenticadas, ya que peticiones anónimas eran interceptadas previamente con `401 Unauthorized` por la `FallbackPolicy` de seguridad global.

---

## Decisión

### 1. Separación Radical de Permisos: Plataforma vs. Tenant
- Se crea el sub-espacio de nombres canónico `Permissions.Platform.Tenants`:
  - `Permissions.Platform.Tenants.View = "Permissions.Platform.Tenants.View"`
  - `Permissions.Platform.Tenants.Create = "Permissions.Platform.Tenants.Create"`
- Se elimina por completo la clase de permisos ambigua `Permissions.Tenants`. Tras auditar el sistema, se confirmó que las operaciones `Tenants.Update` y `Tenants.Delete` tenían 0 implementaciones funcionales en handlers o endpoints CQRS y fueron removidas.
- Se depura `DefaultTenantRoleCatalog`: los roles de tenant (`TenantAdmin`, `Supervisor`, `InventoryManager`, `Cajero`) no poseen ningún permiso que empiece con `Permissions.Platform.*`.

### 2. Catálogo Canónico Único de SuperAdmin en Application (`SuperAdminRoleCatalog`)
- Se define `SuperAdminRoleCatalog` en `Pos.Application.Common.Authorization`:
  ```csharp
  public static class SuperAdminRoleCatalog
  {
      private static readonly HashSet<string> s_permissions =
      [
          Permissions.Platform.DeadLetter.View,
          Permissions.Platform.DeadLetter.Reprocess,
          Permissions.Platform.Tenants.View,
          Permissions.Platform.Tenants.Create
      ];

      public static IReadOnlySet<string> Permissions => s_permissions;
  }
  ```
- Este catálogo es la única fuente de verdad (*Single Source of Truth*) para los privilegios globales del rol `SuperAdmin`.

### 3. Idempotencia del Seeder y Sincronización Atómica de Permisos en Dominio
- Se enriquece la entidad de dominio `Role` con el método de negocio:
  ```csharp
  public void SyncPermissions(IEnumerable<string> targetPermissions);
  ```
  Este método calcula de forma atómica la diferencia de conjuntos (eliminando permisos obsoletos o desalineados y agregando los faltantes), mutando `_permissions` mediante eventos controlados de dominio y manteniendo intacto el identificador y el estado de auditoría.
- `SuperAdminSeeder` consume exclusivamente `SuperAdminRoleCatalog.Permissions`. Si el rol `SuperAdmin` ya existe en la base de datos (incluso si solo tenía asignados los permisos de `DeadLetter`), el seeder sincroniza de forma determinista e idempotente sus permisos para que coincidan exactamente con el catálogo, persistiendo los cambios mediante `SaveChangesAsync()`.
- Se eliminaron todas las re-concesiones manuales de permisos en los fixtures de pruebas (`MsSqlTestFixture`, `AuthLoginSqlServerIntegrationTests`, `PaginationHttpIntegrationTests`). Las pruebas ahora dependen del `SuperAdminSeeder` real.

### 4. Guardas de Arquitectura Automatizadas (`RoleGovernanceTests`)
En `Pos.Architecture.Tests` se añadieron dos pruebas de arquitectura basadas en reflexión y análisis estructural:
1. **`PlatformRequests_HasPermission_MustBeContainedIn_SuperAdminRoleCatalog`:** Escanea todas las solicitudes CQRS ubicadas bajo `Pos.Application.Platform.*` que implementen el atributo `[HasPermission]`. Falla si algún permiso de endpoint de plataforma no se encuentra registrado explícitamente en `SuperAdminRoleCatalog.Permissions`.
2. **`DefaultTenantRoleCatalog_MustNotContain_PlatformPermissions`:** Valida exhaustivamente que ningún rol provisto en `DefaultTenantRoleCatalog.Roles` incluya un permiso que comience con `Permissions.Platform.`. Garantiza que jamás se filtre un permiso de plataforma a un tenant.

### 5. Tipado Estricto de `TenantDto.Status` con `TenantStatus` Enum
- `TenantDto` sustituye el tipo de datos primitivo `string Status` por el enum fuertemente tipado `TenantStatus Status`.
- La configuración de serialización HTTP JSON de la API (`ConfigureHttpJsonOptions`) aplica `JsonStringEnumConverter`, proyectando en el snapshot OpenAPI `openapi.v1.json` un schema con `$ref: "#/components/schemas/TenantStatus"` conteniendo los valores nominales `PendingProvisioning`, `Active`, `Suspended`.
- **Recomendación para `NalunPos-Front`:** En el siguiente paso frontend (F1.D), el cliente debe consumir la definición tipada de `openapi.types.ts` en lugar de literales manuales en TypeScript, evitando divergencias en tiempo de ejecución.

### 6. Política de Exposición de Endpoints en Testing y Production (404 Not Found Estricto)
- Siguiendo la observación F1.A.1, los endpoints `/openapi/v1.json` y `/scalar` sólo se mapean cuando `app.Environment.IsDevelopment()`.
- Bajo entornos `Testing` y `Production`, estos endpoints no existen en la tabla de rutas.
- Las pruebas de integración en `OpenApiContractSnapshotTests` verifican la no-exposición utilizando un `HttpClient` autenticado (`AuthenticatedClientFactory`), asegurando que ASP.NET Core devuelva un código estricto `404 Not Found` en lugar de un `401 Unauthorized` provocado por la `FallbackPolicy`.

---

## Consecuencias

### Positivas
- **Aislamiento Multi-Tenant Estricto:** Imposibilidad arquitectónica de que roles de inquilinos ejecuten comandos de aprovisionamiento o lectura de tenants globales.
- **Operación Real Garantizada:** El usuario `SuperAdmin` sembrado en producción o staging puede operar inmediatamente los endpoints de aprovisionamiento `/api/v1/platform/tenants` sin requerir parches SQL ni scripts manuales posteriores.
- **Idempotencia de Siembra:** Despliegues continuos o migraciones sucesivas actualizan automáticamente los permisos de plataforma en la base de datos sin duplicar registros ni corromper llaves foráneas.
- **Seguridad en Pruebas:** Los fixtures de pruebas prueban el flujo real del seeder sin alterar artificialmente el estado de la base de datos con permisos ad-hoc.
- **Tipado Fuerte en Contratos:** `TenantStatus` se refleja como enum en el contrato OpenAPI, garantizando consistencia semántica en los clientes web y móviles.
