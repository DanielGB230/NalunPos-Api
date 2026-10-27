# ADR 0019: Estrategia de Migraciones en Producción y Workers en Segundo Plano bajo RLS

- **Estado:** Aceptado
- **Fecha:** 2026-10-22
- **Contexto:** Garantizar la integridad del esquema relacional en despliegues concurrentes de producción y definir el patrón arquitectónico determinista para la ejecución de servicios en segundo plano (`BackgroundService` / Workers) que deben operar sobre entidades protegidas por Row-Level Security (RLS) en SQL Server 2022.

---

## 1. Estrategia de Migraciones en Producción (Fail-Fast y Bundle Pre-Despliegue)

### Contexto y Problema Previo
- Anteriormente, el método `MigrateAsync()` se ejecutaba dentro de `Program.cs` envuelto en un bloque `try/catch` que capturaba cualquier `Exception` y únicamente registraba un log de advertencia.
- Esto introducía un comportamiento **Fail-Open** inaceptable en seguridad y gobernanza: si la base de datos no estaba disponible, la conexión fallaba o una migración quedaba incompleta, la API continuaba su arranque con un esquema corrupto o inexistente y sin el usuario `SuperAdmin` inicial sembrado.
- Asimismo, la ejecución de `MigrateAsync()` en tiempo de inicio de múltiples réplicas de aplicación concurrentes en entornos productivos (Kubernetes, App Services o Contenedores) genera condiciones de carrera sobre la tabla `__EFMigrationsHistory`, bloqueos distribuidos y riesgo de interrupción de servicio.

### Decisión
1. **Extracción a `DatabaseInitializer` Fail-Fast:**
   - La lógica de migración y siembra inicial se aísla en `Pos.Api.Extensions.DatabaseInitializer.InitializeDatabaseAsync()`.
   - Queda estrictamente proscrito cualquier bloque `try/catch` silencioso. Si la migración o el `SuperAdminSeeder` fallan, la excepción escala inmediatamente deteniendo el proceso (`Fail-Fast`).
   - En el entorno de desarrollo local (`Development`), la API valida `dbContext.Database.IsRelational()` antes de ejecutar `MigrateAsync()`.

2. **Estrategia de Producción con Herramientas Gratuitas (`dotnet ef migrations bundle`):**
   - En entornos de Producción y Staging, `MigrateAsync()` está desactivado en la aplicación web.
   - Las migraciones se gestionan exclusivamente en el pipeline de CI/CD previo al despliegue de las nuevas réplicas de la aplicación, utilizando el comando gratuito y nativo del SDK de .NET:
     ```bash
     dotnet ef migrations bundle --project src/Pos.Infrastructure --startup-project src/Pos.Api --output ./efbundle --configuration Release --self-contained
     ```
   - El ejecutable generado (`efbundle`) se ejecuta como un paso atómico de migración antes de desplegar o actualizar los pods/instancias de la API, garantizando que ninguna réplica atienda tráfico sin que el esquema esté 100% aplicado y validado.

3. **Interacción con `MsSqlTestFixture` en Pruebas de Integración y CI:**
   - En CI, el paso previo a los tests ejecuta `./efbundle` sobre SQL Server para garantizar la viabilidad del artefacto ejecutable.
   - A continuación, los tests de integración (`Pos.IntegrationTests`) se ejecutan mediante `MsSqlTestFixture`. Para asegurar determinismo hermético e independencia entre ejecuciones locales, el fixture por defecto ejecuta `EnsureDeletedAsync()` seguido de `MigrateAsync()` sobre la base de datos de pruebas (nombre restringido a `*Tests`).
   - Para evitar la destrucción innecesaria de una base pre-migrada en pipelines que así lo requieran, `MsSqlTestFixture` evalúa la variable `IntegrationTests__SkipEnsureDeleted=true`: si está activa, omite `EnsureDeletedAsync()` y ejecuta únicamente `MigrateAsync()` de forma idempotente, preservando el estado inicializado por `efbundle`.

4. **Consolidación Limpia de Migraciones en Fase de Desarrollo (`InitialCreate` Única):**
   - Dado que el sistema se encuentra en fase de desarrollo activo sin datos reales en producción ni datos heredados, se eliminan las cadenas históricas de migraciones incrementales y migraciones de datos con SQL crudo o GUIDs fijos.
   - Todo el modelo relacional, incluyendo las 29 tablas, check constraints (`CK_Users_PlatformRole`, `CK_CashRegisters_BranchId_NotEmpty`), índices y las políticas completas de Row-Level Security (`fn_tenantAccessPredicate`, `fn_userTenantAccessPredicate` y `TenantSecurityPolicy` sobre 24 tablas) quedan formalizados de forma atómica y determinista en una única migración inicial limpia (`InitialCreate`).
   - La recreación de entornos de desarrollo local se efectúa mediante `dotnet ef database drop --force` seguido de `dotnet ef database update`.

---

## 2. Patrón de Workers en Segundo Plano Cross-Tenant bajo RLS

### Contexto y Problema Previo
- Las entidades de negocio (incluyendo `Invoice`, `StockMovement`, `Sale`, etc.) implementan `ITenantOwnedEntity` y están protegidas por Row-Level Security (`TenantSecurityPolicy`) a nivel de SQL Server 2022.
- Si un worker en segundo plano (como `InvoiceReconciliationBackgroundService`) ejecuta consultas usando un `DbContext` sin haber establecido `TenantId` ni `IsSuperAdmin` en el `SESSION_CONTEXT` de SQL Server, la política de seguridad RLS bloquea todas las filas devolviendo 0 registros, a pesar del uso de `IgnoreQueryFilters()` en LINQ (el cual solo omite el filtro de EF Core, no la política RLS del motor relacional).

### Decisión
1. **Separación de Responsabilidades y Puertos de Plataforma:**
   - `Pos.Api` y sus `BackgroundServices` solo actúan como temporizadores y orquestadores de scopes; **prohibida la lógica de negocio en la API**.
   - Para la lectura cross-tenant de registros pendientes que requieren conciliación o procesamiento global, se define un puerto en `Pos.Application` (ej. `IPendingInvoicePlatformReader`), implementado en `Pos.Infrastructure`.
   - El puerto activa temporal y exclusivamente el alcance de plataforma en un bloque controlado:
     ```csharp
     tenantSetter.SetSuperAdmin(true);
     try
     {
         // Lectura autorizada con IgnoreQueryFilters() bajo contexto SESSION_CONTEXT IsSuperAdmin=1
     }
     finally
     {
         tenantSetter.SetSuperAdmin(false);
     }
     ```
2. **Aislamiento por Scope para Procesamiento de Negocio:**
   - Para cada elemento descubierto por el lector de plataforma `(EntityId, TenantId)`, el worker crea un `IServiceScope` nuevo y limpio.
   - En dicho scope, se establece `tenantSetter.SetTenantId(tenantId)` y `tenantSetter.SetSuperAdmin(false)`.
   - Se despacha el caso de uso correspondiente a través de `IDispatcher` (ej. `ReconcileInvoiceCommand`), asegurando que toda la ejecución de dominio y persistencia ocurra con aislamiento estricto de tenant bajo RLS.

---

## Consecuencias

- **Positivas:**
  - Cero arranques en estado inválido (Fail-Fast garantizado por pruebas automatizadas).
  - Despliegues continuos seguros en producción sin condiciones de carrera en migraciones concurrentes.
  - Workers en segundo plano compatibles con la seguridad RLS de SQL Server 2022, sin vulnerar el aislamiento multi-tenant.
- **Negativas:**
  - Requiere incluir el paso de ejecución de `efbundle` en los scripts de despliegue de infraestructura de producción.
