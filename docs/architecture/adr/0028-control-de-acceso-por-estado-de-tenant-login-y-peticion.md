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

`ITenantStatusCacheInvalidator` (puerto en `Pos.Application/Common/Interfaces`, implementación
`TenantStatusCacheInvalidator` en `Pos.Infrastructure/Caching`) expone `InvalidateAsync(tenantId, cancellationToken)`:
resuelve `HybridCache` y elimina la clave del estado del tenant. Los handlers `SuspendTenant` y `ActivateTenant` lo
invocan **después** de persistir con éxito; si el cambio de estado falla o la transición es inválida, no se invalida.
Si la invalidación lanza una excepción, el cambio ya está confirmado: se registra con `ILogger` y no se revierte.

TTL configurable vía `TenantStatusOptions.TenantStatusCacheTtlSeconds` (por defecto 60 s). El TTL actúa como **cota de
seguridad**, no como retardo esperado: la invalidación explícita propaga el cambio de inmediato en la instancia que lo
aplica, y el TTL solo acota el retardo en otras instancias o si la invalidación falla. Mismo criterio que los permisos
(ADR 0015).

### Vocabulario de errores

Se agregó `ErrorType.Forbidden` a `DomainError`, mapeado a HTTP 403 por `PosProblemDetailsFactory` (sin duplicar mapeos).

### ¿Middleware y no handlers?

- El middleware aplica la política uniformemente a todas las peticiones autenticadas de tenant sin tocar cada handler.
- `LoginCommandHandler` aplica la política solo en el login (después de la contraseña).
- Los handlers de aplicación (`ActivateTenant`, `SuspendTenant`) no leen `Tenant.Status` para decisiones de acceso;
  solo `TenantAccessPolicy`, `TenantStatusTransitions` y `TenantStatusReader` lo hacen (verifica con la guarda de
  arquitectura `TenantStatus_MustOnlyBeReadBy_DomainPolicyAndTransitions`).

### Deuda explícita

1. **Auditoría de accesos denegados:** no se registra quién intentó acceder a un tenant suspendido y cuándo.
2. **Mensaje en el front:** el cliente recibe 403 con código, pero no hay superficie de UI específica para presentar
   el estado de suspensión al usuario final.

## Consecuencias

### Positivas
- Solo un tenant `Active` puede autenticarse y operar; `PendingProvisioning` y `Suspended` reciben 403 con código estable.
- Las credenciales inválidas contra un tenant suspendido devuelven 401 genérico (no se revela el estado sin credenciales válidas).
- SuperAdmin no se ve afectado por el estado de ningún tenant.
- El cambio de estado se propaga de inmediato en la instancia que lo aplica (invalidación explícita); el TTL (60 s por defecto, configurable) es la cota de seguridad.
- Cero cambios de modelo de base de datos (`has-pending-model-changes` negativo).

### Negativas / Trade-offs
- Un token emitido antes de la suspensión deja de operar en la siguiente petición atendida por la instancia que aplicó el cambio; en otras instancias sigue vigente hasta que expire el TTL de caché.
- No hay revocación inmediata e independiente de sesiones activas (blacklist de tokens); la suspensión se refleja vía estado del tenant.
- `HybridCache` configurado solo en memoria: la invalidación afecta únicamente a la instancia local. En despliegues multi-instancia, el TTL sigue acotando la propagación entre instancias.
- `NalunPos-Front` no se modifica (fuera de alcance).

### Pruebas

Las pruebas de integración no dependen del TTL: cuando el cambio de estado pasa por la API (`SuspendTenant`/`ActivateTenant`),
los handlers invalidan la caché y las pruebas verifican el nuevo estado sin invalidación manual. Solo el escenario que
borra un tenant directamente en BD (sin handlers) conserva `TenantStatusCacheControl.InvalidateAsync(...)` para forzar
la lectura fresca.
