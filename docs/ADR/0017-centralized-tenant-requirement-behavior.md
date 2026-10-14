# ADR 0017: Requerimiento Centralizado de Tenant en Pipeline CQRS (Default-Deny) e Interceptor Fail-Closed

- **Estado:** Aceptado
- **Fecha:** 2026-10-14
- **Contexto:** En NalunPos-Api, diversas operaciones de escritura en `src/Pos.Application` requerían un tenant activo, pero la validación se realizaba de manera dispersa o mediante fallbacks engañosos (por ejemplo, `_tenantContext.TenantId ?? Guid.Empty` o fabricación arbitraria con `Guid.NewGuid()`). Esto provocaba que si una solicitud carecía de tenant (como un SuperAdmin ejecutando comandos de tenant o tokens sin claim de tenant), la operación progresaba hasta el dominio o los repositorios arrojando excepciones engañosas de validación de entidad en lugar de identificar la falta del contexto de aislamiento. Asimismo, tras ADR 0016 y la unificación de atributos, la autorización (`[HasPermission]`) vive exclusivamente en el request del comando/query (`ICommand`/`IQuery`). Se requería un modelo default-deny centralizado para los comandos de escritura, complementado por un interceptor fail-closed en EF Core para la persistencia.

## Decisión

Hemos decidido implementar un esquema estricto de **Default-Deny** en el pipeline CQRS junto a un contrato explícito de entidades con alcance de plataforma (`IPlatformScopedEntity`) y un interceptor fail-closed en EF Core:

1. **Default-Deny en Comandos CQRS (`[RequiresTenant]` y `[AllowsNoTenant]`):**
   - Todo comando (`ICommand` / `ICommand<TResponse>`) en `src/Pos.Application` DEBE estar decorado obligatoriamente y de manera mutuamente excluyente con:
     - `[RequiresTenant]`: el comando requiere un inquilino activo en contexto.
     - `[AllowsNoTenant]`: reservado exclusivamente para operaciones de plataforma o autenticación previa (e.g. `LoginCommand`, `CreateTenantCommand`).
   - Queda estrictamente prohibido colocar `[RequiresTenant]` o `[AllowsNoTenant]` en clases terminadas en `*Handler`.
   - Se impone la verificación determinista en arquitectura mediante `CommandTenantRequirementScanner` y `HandlerRequiresTenantAttributeScanner`.

2. **`TenantRequirementBehavior<,>` en Pipeline CQRS (Fail-Closed):**
   - Se ejecuta antes del validador de negocio y del handler:
     `LoggingDecorator` → `AuthorizationBehavior` → `TenantRequirementBehavior` → `ValidationDecorator` → `InnerHandler`.
   - Si el comando tiene `[RequiresTenant]` y el contexto actual `ICurrentTenantContext` no posee un `TenantId` válido (es `null` o `Guid.Empty`), interrumpe inmediatamente el pipeline sin invocar al handler y retorna un `Result` fallido con el error de dominio `tenant.required` (`ErrorType.Validation`).

3. **Defensa en Profundidad en Handlers (`RequireTenantId()`):**
   - Se implementa el método de extensión `context.RequireTenantId() -> Guid`.
   - Lanza `InvalidOperationException` si se invoca sin un tenant resuelto válido (violación de invariante).
   - Erradica de raíz el uso de `_tenantContext.TenantId ?? Guid.Empty`, operadores `!` y fabricaciones de tenant con `Guid.NewGuid()`.

4. **Contrato de Alcance de Plataforma (`IPlatformScopedEntity`) y Sentinela Nombrada (`PlatformScope.TenantId`):**
   - Según ADR 0008, el filtro global de `PosDbContext` (`SetTenantFilter`) y la política RLS definen `TenantId == Guid.Empty` como el alcance de plataforma cuando no hay inquilino en contexto (`_currentTenantId == null`).
   - Para erradicar el uso del literal `Guid.Empty` suelto como valor mágico en comparaciones y asignaciones de alcance global, se define la constante formal de dominio:
     ```csharp
     namespace Pos.Domain.Common;
     public static class PlatformScope
     {
         public static readonly Guid TenantId = Guid.Empty;
     }
     ```
   - Interfaz marcadora de dominio:
     ```csharp
     namespace Pos.Domain.Common;
     public interface IPlatformScopedEntity : ITenantOwnedEntity
     {
     }
     ```
   - **Lista Cerrada:** Únicamente `Role` y `AuditLog` pueden implementar `IPlatformScopedEntity`. Una guarda de arquitectura Roslyn (`PlatformScopedEntityScanner`) garantiza que cualquier otro tipo que intente implementarla cause un fallo de compilación/test de arquitectura.

5. **`TenantSaveChangesInterceptor` Fail-Closed en EF Core:**
   - Durante `SavingChanges` y `SavingChangesAsync`, para toda entidad `Added` que implemente `ITenantOwnedEntity`:
     - Si hay un tenant activo en contexto (`TenantId != null && TenantId != PlatformScope.TenantId`): asigna dicho `TenantId` si venía vacío.
     - Si NO hay un tenant activo en contexto (`TenantId == null || TenantId == PlatformScope.TenantId`):
       - Si la entidad implementa `IPlatformScopedEntity` (`Role`, `AuditLog`): permite persistir con `TenantId == PlatformScope.TenantId` como sentinela de plataforma.
       - Si la entidad NO implementa `IPlatformScopedEntity` (e.g. `Branch`, `Warehouse`, `Product`): lanza `InvalidOperationException` impidiendo la inserción en base de datos sin contexto de inquilino.

6. **Guardas de Arquitectura con Roslyn (`TenantGovernanceTests`):**
   - **Regla 1:** Prohibido el uso de `TenantId ?? Guid.Empty` y `Guid.NewGuid()` para variables/campos de tenant en `src/Pos.Application` (`TenantFallbackScanner`).
   - **Regla 2:** Todo handler de comando que invoque `RequireTenantId()` debe tener su comando anotado con `[RequiresTenant]` (`HandlerTenantRequirementScanner`).
   - **Regla 3:** Los atributos `[RequiresTenant]` y `[AllowsNoTenant]` están prohibidos en cualquier tipo `*Handler` (`HandlerRequiresTenantAttributeScanner`).
   - **Regla 4:** Todo comando debe tener `[RequiresTenant]` o `[AllowsNoTenant]` de forma mutuamente excluyente (`CommandTenantRequirementScanner`).
   - **Regla 5:** Lista cerrada estricta para `IPlatformScopedEntity`, restringida únicamente a `Role` y `AuditLog` (`PlatformScopedEntityScanner`).
   - **Regla 6:** Prohibido el uso de `Guid.Empty` como sentinela de tenant en `src/Pos.Infrastructure` y `src/Pos.Application`, obligando a usar `PlatformScope.TenantId` (`PlatformScopeSentinelScanner`).

## Consecuencias

### Positivas
- **Fail-Closed Centralizado y Default-Deny**: Ningún comando de tenant se ejecuta sin contexto válido; el cliente recibe un error HTTP 400 (`tenant.required`) estructurado según RFC 9110 / RFC 9457 sin depender de validaciones tardías.
- **Aislamiento en Persistencia**: Si una entidad del tenant llegase a crearse indebidamente sin contexto (por ejemplo por un bypass en la capa de aplicación), el interceptor de EF Core aborta la transacción antes de llegar a SQL.
- **Transparencia en Operaciones de Plataforma**: Las operaciones globales del SuperAdmin y el registro de auditoría (`AuditLog`) continúan funcionando en el alcance de plataforma con `TenantId == PlatformScope.TenantId` de acuerdo con ADR 0008, sin comprometer el aislamiento del resto de entidades.
- **Sentinela Nombrada Fuertemente Tipada**: Se elimina la ambigüedad del valor mágico `Guid.Empty` en infraestructura y aplicación mediante `PlatformScope.TenantId`.
- **Gobierno Automatizado**: Pruebas basadas en Roslyn previenen regresiones en tiempo de compilación y CI.

### Límites Conocidos Aceptados (Diferidos)
- **Sobrecargas de Factoría en Dominio (Paso 10.2)**: 12 entidades de tenant aún poseen métodos de creación factoría `Create` sin parámetro `tenantId` (o sobrecargas duales que inicializan con `Guid.Empty`), dependiendo exclusivamente del `TenantSaveChangesInterceptor`. La eliminación o refactorización de estas sobrecargas para requerir explícitamente `tenantId` en la firma de dominio se aborda en el Paso 10.2.
