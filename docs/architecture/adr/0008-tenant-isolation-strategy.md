# ADR 0008: Estrategia de Aislamiento de Tenant (Shared Database + Global Query Filters)

- **Estado:** Aceptado
- **Fecha:** 2026-09-01
- **Contexto:** El sistema evoluciona a un SaaS multi-tenant donde múltiples empresas operan sobre una única base de datos compartida y un único backend.

## Decisión
Adoptar el patrón **Shared Database, Shared Schema** con discriminante `TenantId` e implementación de **Defensa en Profundidad**:

1. **`ITenantOwnedEntity` en Domain:** Marcador de contrato para todas las entidades que pertenecen a un tenant.
2. **Global Query Filters en EF Core:** Filtro automático a nivel de `DbContext` para que las consultas lean únicamente datos del tenant actual resuelto.
3. **`TenantSaveChangesInterceptor` en Infrastructure:** Asignación automática del `TenantId` al crear entidades, evitando asignaciones manuales en los handlers.
4. **`TenantResolutionMiddleware` en API:** Resolución del `TenantId` exclusivamente a partir de los claims del JWT autenticado. Queda estrictamente prohibido aceptar `TenantId` desde el cliente (headers, query params o body).

## Consecuencias
- **Positivas:** Bajo costo operativo, mantenimiento simplificado, aislamiento garantizado a nivel estructural.
- **Negativas:** Requiere precaución si se ejecutan consultas que omitan filtros (`IgnoreQueryFilters`), reservado únicamente para operaciones explícitas de plataforma (SuperAdmin).
