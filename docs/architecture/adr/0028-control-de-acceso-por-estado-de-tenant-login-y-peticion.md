# ADR 0028 — Control de acceso por estado de tenant (login y petición)

## Estado

Aceptado

## Contexto

Un tenant puede estar en los estados `PendingProvisioning`, `Active` o `Suspended` (ADR 0027). Hasta este paso,
ningún componente leía `Tenant.Status` para decidir si un usuario podía autenticarse o operar. `LoginCommandHandler`
solo verificaba `User.IsActive` y `TenantResolutionMiddleware` solo resolvía claims.

## Decisión

Se aplica el control de acceso por estado de tenant en dos puntos:

1. **Login (después de verificar la contraseña):** si `user.TenantId` tiene valor, se consulta el estado del tenant
   mediante el puerto `ITenantStatusReader` y se aplica `TenantAccessPolicy.ValidateAccess()`. Si deniega, no se
   emite token y se devuelve el `Result` de error con código estable. SuperAdmin (`TenantId == null`) no se ve afectado.
2. **Cada petición autenticada:** el middleware `TenantStatusEnforcementMiddleware` (registrado entre
   `TenantResolutionMiddleware` y `UseAuthorization`) verifica que el tenant del usuario autenticado esté activo.
   Si la política deniega, responde `403 ProblemDetails` con extensión `code` sin lanzar excepciones como flujo de
   control.

Solo actúa cuando la petición está autenticada, tiene `TenantId` y no es SuperAdmin. Endpoints anónimos y SuperAdmin
no se tocan.

### Fuente única de la regla

`TenantAccessPolicy` en `Pos.Domain/Common` (mismo estilo que `TenantStatusTransitions`):
- `Active` → permitido
- `Suspended` → `Tenant.Suspended` (403)
- `PendingProvisioning` → `Tenant.NotActive` (403)
- `null` o valor de enum no reconocido → `Tenant.NotActive` (fail-closed)

### Puerto y caché

`ITenantStatusReader` en `Pos.Application/Common/Interfaces`, implementado en `Pos.Infrastructure/Caching/TenantStatusReader`
con `HybridCache`. La tabla `Tenants` NO está bajo RLS, por lo que la lectura por `tenantId` es válida desde cualquier
contexto, incluidos callbacks de `HybridCache` bajo `ExecutionContext.SuppressFlow()` (no requiere `ITenantSetter`).

Clave de caché: `tenant:{tenantId:D}:status` (en `TenantCacheKeys`, ADR 0011).

TTL configurable vía `TenantStatusOptions.TenantStatusCacheTtlSeconds` (por defecto 60 s). El TTL define el retardo
máximo de propagación de un cambio de estado (suspender/reactivar) hasta que surta efecto en peticiones autenticadas.
Mismo criterio que los permisos (ADR 0015).

### Vocabulario de errores

Se agregó `ErrorType.Forbidden` a `DomainError`, mapeado a HTTP 403 por `PosProblemDetailsFactory` (sin duplicar mapeos).

### ¿Middleware y no handlers?

- El middleware aplica la política uniformemente a todas las peticiones autenticadas de tenant sin tocar cada handler.
- `LoginCommandHandler` aplica la política solo en el login (después de la contraseña).
- Los handlers de aplicación (`ActivateTenant`, `SuspendTenant`) no leen `Tenant.Status` para decisiones de acceso;
  solo `TenantAccessPolicy`, `TenantStatusTransitions` y `TenantStatusReader` lo hacen (verifica con la guarda de
  arquitectura `TenantStatus_MustOnlyBeReadBy_DomainPolicyAndTransitions`).

### Deuda explícita

1. **Revocación inmediata de tokens/sesiones (G.3 y Fase S):** un token ya emitido sigue siendo válido hasta que expire
   el TTL de la caché o se reinicie la sesión. La revocación inmediata (blacklist de tokens, inyección de claims de
   estado) queda pendiente.
2. **Auditoría de accesos denegados:** no se registra quién intentó acceder a un tenant suspendido y cuándo.
3. **Mensaje en el front:** el cliente recibe 403 con código, pero no hay superficie de UI específica para presentar
   el estado de suspensión al usuario final.

## Consecuencias

### Positivas
- Solo un tenant `Active` puede autenticarse y operar; `PendingProvisioning` y `Suspended` reciben 403 con código estable.
- Las credenciales inválidas contra un tenant suspendido devuelven 401 genérico (no se revela el estado sin credenciales válidas).
- SuperAdmin no se ve afectado por el estado de ningún tenant.
- El retardo máximo de propagación está acotado por el TTL de caché (60 s por defecto, configurable).
- Cero cambios de modelo de base de datos (`has-pending-model-changes` negativo).

### Negativas / Trade-offs
- Un token emitido antes de la suspensión funciona hasta que expira el TTL de caché.
- No hay revocación inmediata de sesiones activas.
- `NalunPos-Front` no se modifica (fuera de alcance).

### Pruebas

Las pruebas de integración no dependen del TTL: invalidan la caché de estado de tenant de forma explícita con
`TenantStatusCacheControl.InvalidateAsync(...)` en el punto exacto donde el escenario cambia el estado. La
invalidación desde producción sigue siendo la deuda 1 de este ADR (sin cambios).
