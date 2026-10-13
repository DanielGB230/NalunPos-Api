# ADR 0017: Requerimiento Centralizado de Tenant en Pipeline CQRS (Fail-Closed)

- **Estado:** Aceptado
- **Fecha:** 2026-10-13
- **Contexto:** En NalunPos-Api, diversas operaciones de escritura en `src/Pos.Application` requerían un tenant activo, pero la validación se realizaba de manera dispersa o mediante fallbacks engañosos (por ejemplo, `_tenantContext.TenantId ?? Guid.Empty` o fabricación arbitraria con `Guid.NewGuid()`). Esto provocaba que si una solicitud carecía de tenant (como un SuperAdmin ejecutando comandos de tenant o tokens sin claim de tenant), la operación progresaba hasta el dominio o los repositorios arrojando excepciones engañosas de validación de entidad en lugar de identificar la falta del contexto de aislamiento. Asimismo, tras ADR 0016 y la unificación de atributos, la autorización (`[HasPermission]`) vive exclusivamente en el request del comando/query (`ICommand`/`IQuery`). Se requiere un modelo análogo, centralizado y fail-closed para exigir el tenant en comandos de escritura.

## Decisión

Hemos decidido centralizar la exigencia de contexto de tenant en el pipeline CQRS mediante el atributo marcador `[RequiresTenant]`, el behavior `TenantRequirementBehavior` y la extensión de defensa en profundidad `RequireTenantId()`:

1. **Atributo Marcador `[RequiresTenant]` en el Request**:
   - Se ubica exclusivamente en el record o struct del comando (`ICommand<TResponse>`).
   - Queda estrictamente prohibido colocar `[RequiresTenant]` en clases terminadas en `*Handler`.

2. **`TenantRequirementBehavior<,>` en Pipeline CQRS (Fail-Closed)**:
   - Se ejecuta antes del validador de negocio y del handler.
   - Orden del pipeline de comandos:
     `LoggingDecorator` → `AuthorizationBehavior` → `TenantRequirementBehavior` → `ValidationDecorator` → `InnerHandler`.
   - Si el comando tiene `[RequiresTenant]` y el contexto actual `ICurrentTenantContext` no posee un `TenantId` válido (es `null` o `Guid.Empty`), interrumpe inmediatamente el pipeline sin invocar al handler y retorna un `Result` fallido con el error de dominio estable `tenant.required` (`ErrorType.Validation`).

3. **Defensa en Profundidad en Handlers (`RequireTenantId()`):**
   - Se implementa el método de extensión `context.RequireTenantId() -> Guid`.
   - Lanza `InvalidOperationException` si se invoca sin un tenant resuelto válido (violación de invariante).
   - Erradica de raíz el uso de `_tenantContext.TenantId ?? Guid.Empty`, operadores `!` y fabricaciones de tenant con `Guid.NewGuid()`.

4. **Defensa en Profundidad en Capas Inferiores:**
   - La base de datos y la infraestructura mantienen Row Level Security (RLS), `TenantSaveChangesInterceptor`, y los Global Query Filters de EF Core como defensas en profundidad para garantizar que ninguna consulta o inserción traspase límites de tenant incluso si fallara una capa superior.
   - Las consultas (queries) quedan fuera de este behavior de comandos, ya que los Global Query Filters y RLS aíslan estrictamente el acceso de lectura.

5. **Guardas de Arquitectura con Roslyn (`TenantGovernanceTests`):**
   - La regla se impone y verifica de manera determinista en `tests/Pos.Architecture.Tests`:
     - **Regla 1:** Prohibido el uso de `TenantId ?? Guid.Empty` y `Guid.NewGuid()` para variables/campos de tenant en `src/Pos.Application` (`TenantFallbackScanner`).
     - **Regla 2:** Todo handler de comando que invoque `RequireTenantId()` debe tener su comando anotado con `[RequiresTenant]` (`HandlerTenantRequirementScanner`).
     - **Regla 3:** El atributo `[RequiresTenant]` está prohibido en cualquier tipo `*Handler` (`HandlerRequiresTenantAttributeScanner`).

## Consecuencias

### Positivas
- **Fail-Closed Centralizado**: Ningún comando de tenant se ejecuta sin contexto válido; el cliente recibe un error HTTP 400 (`tenant.required`) claro y estructurado según RFC 9110 / RFC 9457 sin depender de validaciones tardías en el agregador de dominio.
- **Transparencia en Handlers**: Los handlers acceden al tenant de forma limpia y tipada mediante `RequireTenantId()`, sin operadores de fallback engañosos (`?? Guid.Empty`).
- **Verificación Automatizada**: Tres escáneres Roslyn en las pruebas de arquitectura impiden regresiones durante compilación y CI.

### Negativas / Límites Aceptados
- Los comandos que requieran operar a nivel de tenant deben declarar obligatoriamente el atributo marcador `[RequiresTenant]` en su record.
