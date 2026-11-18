# ADR 0027: Ciclo de Vida del Tenant — Transiciones de Estado, Autorización y Deuda de Enforcement

- **Estado:** Aceptado
- **Fecha:** 2026-11-18
- **Contexto:**
  1. El agregado `Tenant` ya modelaba los estados `PendingProvisioning`, `Active` y `Suspended`, pero no existía ningún caso de uso, comando, permiso ni endpoint para abandonar el estado inicial. Todo tenant aprovisionado por `POST /api/v1/platform/tenants` permanecía indefinidamente en `PendingProvisioning`.
  2. `Tenant.Activate` y `Tenant.Suspend` existían como métodos `void` idempotentes: una activación repetida no fallaba ni emitía un nuevo evento. Esa semántica impedía distinguir una operación válida de un conflicto real y dejaba la regla de transiciones dispersa en el agregado.
  3. El plano de control ya separa los permisos de plataforma (`Permissions.Platform.*`) del catálogo de roles de tenant (`DefaultTenantRoleCatalog`). Cualquier permiso nuevo debía respetar esa separación y quedar contenido en `SuperAdminRoleCatalog`.
  4. El alcance de este paso es exclusivamente habilitar las operaciones de plataforma para activar y suspender tenants. El bloqueo efectivo del login, la autorización por petición y la vigencia del token pertenecen a pasos posteriores (G.2 y G.3) y no se implementan aquí.

---

## Decisión

### 1. Máquina de estados y transiciones permitidas
Se adopta la siguiente matriz como contrato del ciclo de vida:

| Estado actual | Acción | Estado destino | Resultado |
|---|---|---|---|
| `PendingProvisioning` | Activar | `Active` | Permitida |
| `Active` | Suspender | `Suspended` | Permitida |
| `Suspended` | Activar | `Active` | Permitida |
| `Active` | Activar | `Active` | Conflicto `Tenant.AlreadyActive` |
| `Suspended` | Suspender | `Suspended` | Conflicto `Tenant.AlreadySuspended` |
| `PendingProvisioning` | Suspender | `Suspended` | Conflicto `Tenant.InvalidTransition` |

`PendingProvisioning` → `Suspended` no está permitida: un tenant debe activarse antes de poder suspenderse. Cualquier transición no contemplada se rechaza con `DomainError.Conflict` y código estable `Tenant.InvalidTransition`.

### 2. Fuente única de verdad en dominio
- Se crea `Pos.Domain.Common.TenantStatusTransitions` como único lugar donde se declara la matriz y los códigos estables:
  - `Tenant.InvalidTransition`
  - `Tenant.AlreadyActive`
  - `Tenant.AlreadySuspended`
- `Tenant.Activate(TimeProvider)` y `Tenant.Suspend(TimeProvider)` pasan de `void` idempotente a `Result` no idempotente:
  - Validan la transición con `TenantStatusTransitions`.
  - Si es inválida o repetida, retornan `Result.Failure` con el error estable y no mutan el estado ni emiten eventos.
  - Si es válida, mutan `Status`, actualizan `UpdatedAtUtc` y emiten `TenantStatusChangedDomainEvent`.
- **Cambio de comportamiento reportado:** se elimina la idempotencia silenciosa de `Activate`/`Suspend`. Una repetición ahora es un conflicto explícito, lo que permite que el borde HTTP responda `409 Conflict` con un código distinguible. Las pruebas de dominio se ajustaron para reflejar el nuevo contrato.

### 3. Casos de uso de plataforma
- `ActivateTenantCommand(Guid Id) : ICommand<Result<bool>>`
- `SuspendTenantCommand(Guid Id) : ICommand<Result<bool>>`
- Ambos comandos declaran `[AllowsNoTenant]` (operan sobre el plano de control sin tenant resuelto) y `[HasPermission(Permissions.Platform.Tenants.Update)]`.
- Cada handler:
  1. Obtiene el tenant por ID.
  2. Si no existe, retorna `DomainError.NotFound` con código `Tenant.NotFound`.
  3. Delega la transición al agregado; si falla, propaga el error estable.
  4. Persiste el cambio mediante `IUnitOfWork.SaveChangesAsync`.
- Cada comando tiene su validador FluentValidation en archivo propio, conforme a la guarda `CqrsValidationGovernanceTests`.

### 4. Permiso de plataforma y catálogo SuperAdmin
- Se agrega `Permissions.Platform.Tenants.Update = "Permissions.Platform.Tenants.Update"` al sub-espacio de permisos de plataforma.
- Se incorpora a `SuperAdminRoleCatalog.Permissions`; el `SuperAdminSeeder` sincroniza el rol persistido al arrancar.
- Ningún rol de `DefaultTenantRoleCatalog` recibe el permiso. Las guardas `RoleGovernanceTests` lo verifican:
  - Todo `[HasPermission]` bajo `Pos.Application.Platform.*` debe estar en `SuperAdminRoleCatalog`.
  - Ningún rol de tenant puede contener permisos `Permissions.Platform.*`.

### 5. Contrato HTTP
- Se expone `PATCH /api/v1/platform/tenants/{id}/status` en `TenantsController`, ultra-delgado y delegando en `IDispatcher`.
- El cuerpo usa `ChangeStatusRequest(bool IsActive)`, serializado como JSON booleano (`{ "isActive": true }` activa, `{ "isActive": false }` suspende).
- No hay validador de borde en el controlador: el mapeo `isActive=true → ActivateTenantCommand`, `isActive=false → SuspendTenantCommand` se hace directamente en el controlador, igual que `UsersController.ChangeUserStatus`.
- Respuestas documentadas: `200` (transición aplicada), `401`, `403`, `404` (`Tenant.NotFound`) y `409` (`Tenant.InvalidTransition`, `Tenant.AlreadyActive`, `Tenant.AlreadySuspended`).
- La autorización de controlador sigue siendo `[Authorize(Roles = SuperAdmin)]`: un `TenantAdmin` real recibe `403 Forbidden` antes de ejecutar el comando.

### 6. Eventos
- La transición exitosa emite `TenantStatusChangedDomainEvent(TenantId, OldStatus, NewStatus, OccurredOnUtc)`.
- El interceptor de outbox ya traduce ese evento a `TenantStatusChangedIntegrationEventV1`, por lo que los consumidores reciben el cambio de estado sin acoplar el endpoint a la infraestructura de mensajería.
- Las transiciones rechazadas no emiten eventos ni actualizan `UpdatedAtUtc`.

---

## Deuda explícita

1. **Enforcement de login y autorización por petición (G.2):** este ADR solo habilita el cambio de estado. Un tenant `Suspended` todavía puede iniciar sesión y usar un token vigente; el bloqueo efectivo en autenticación, resolución de tenant y autorización por petición queda para G.2. Hasta entonces, la suspensión es una señal de control plane, no un corte de acceso.
2. **Vigencia del token (G.3):** la suspensión no invalida tokens ya emitidos. La revocación/expiración forzada pertenece a G.3.
3. **Baja y offboarding con retención:** no se introduce un estado `Deleted` ni un proceso de baja. Quedan pendientes la retención legal de datos, exportación, anonimización, eliminación física o lógica y los plazos por jurisdicción.
4. **Auditoría de cambios de estado:** el evento de dominio y el outbox registran el cambio, pero no existe una consulta o superficie de auditoría que responda quién cambió el estado, cuándo y desde qué canal. Persistir el actor y exponer el historial queda como deuda.
5. **Sincronización del frontend:** `NalunPos-Front` consumirá la nueva operación OpenAPI en un paso separado; este cambio es exclusivamente backend.

---

## Consecuencias

### Positivas
- El ciclo de vida del tenant deja de ser implícito: la matriz, los códigos de error y los eventos están centralizados y cubiertos por pruebas de dominio, handlers e integración HTTP contra SQL Server real.
- El SuperAdmin puede activar, suspender y reactivar tenants sin intervención manual en base de datos, con respuestas `409` distinguibles para repeticiones y transiciones inválidas.
- La separación de permisos se preserva: ningún rol de tenant puede ejecutar operaciones de plataforma.
- El contrato OpenAPI documenta la operación y el snapshot queda versionado y verificado por SHA256.

### Negativas / Trade-offs
- Se rompe la idempotencia previa de `Tenant.Activate`/`Tenant.Suspend`: los llamadores que dependían de repetir la operación sin error ahora reciben un `Result` fallido. Es intencional y está reportado.
- La suspensión no bloquea el acceso hasta G.2; operar el estado sin enforcement puede dar una falsa sensación de corte inmediato.
- La auditoría de actor y la baja con retención siguen abiertas, por lo que el ciclo de vida aún no es completo para offboarding.
