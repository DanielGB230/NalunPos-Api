# ADR 0008: Estrategia de Aislamiento de Tenant (Shared Database + Global Query Filters)

- **Estado:** Aceptado
- **Fecha:** 2026-09-01
- **Contexto:** El sistema evoluciona a un SaaS multi-tenant donde múltiples empresas operan sobre una única base de datos compartida y un único backend.

## Decisión
Adoptar el patrón **Shared Database, Shared Schema** con discriminante `TenantId` e implementación de **Defensa en Profundidad**:

1. **`ITenantOwnedEntity` en Domain:** Marcador de contrato para todas las entidades que pertenecen a un tenant.
2. **Global Query Filters en EF Core:** Filtro automático a nivel de `DbContext` para que las consultas lean únicamente datos del tenant actual resuelto.
3. **`TenantSaveChangesInterceptor` en Infrastructure:** Asignación automática del `TenantId` al crear entidades, evitando asignaciones manuales en los handlers.
4. **`TenantResolutionMiddleware` + `TenantClaimsResolver` + `ITenantSetter` en API/Infraestructura:** Resolución del `TenantId` exclusivamente a partir de los claims del JWT autenticado (`tenantId`/`tenant_id`/`tid`) mediante `TenantClaimsResolver`, fijando el estado explícitamente en el scope de la petición vía `ITenantSetter` para que `CurrentTenantContext` mantenga getters puros (sin snapshots mutables) incluso bajo `ExecutionContext.SuppressFlow()` de `HybridCache`. Queda estrictamente prohibido aceptar `TenantId` desde el cliente (headers, query params o body).
5. **`User`/`Role` Query Filter & Puerto de Autenticación (`IAuthUserLookup` + `IAuthenticationSessionScope`):** Las consultas sobre `User` y `Role` aplican aislamiento estricto por tenant y RLS. Durante el Login (cuando aún no existe tenant en el request), `IAuthUserLookup` (`AuthUserLookup`) se apoya en `IAuthenticationSessionScope` (`SqlServerAuthenticationSessionScope`) para: (a) habilitar de forma temporal y acotada `SESSION_CONTEXT('AllowGlobalUserLookup') = 1` solo al buscar el usuario por email (deshabilitándolo en `finally`), y (b) acotar luego el `SESSION_CONTEXT` al `TenantId` del usuario para resolver de forma segura el nombre canónico de su `Role` bajo RLS (o validar estrictamente `SuperAdminRoleId && TenantId == null` para plataforma), restaurando el estado previo de sesión y conexión al finalizar. Quedan proscritos los `IgnoreQueryFilters()` sueltos en repositorios de negocio y los `try/catch` por tipo de proveedor.

## Consecuencias
- **Positivas:** Bajo costo operativo, mantenimiento simplificado, aislamiento garantizado a nivel estructural en EF Core y SQL Server RLS, y resolución determinista tanto de usuario como de rol canónico durante la autenticación (ver ADR 0023).
- **Negativas:** Operaciones de autenticación y plataforma cross-tenant deben encapsularse en puertos dedicados (`IAuthUserLookup`, `IAuthenticationSessionScope`), previniendo fugas accidentales.
