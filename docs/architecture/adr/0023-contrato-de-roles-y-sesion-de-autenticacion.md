# ADR 0023: Contrato Canónico de Roles, Resolución de Identidad Fail-Closed y Sesión de Autenticación bajo RLS

- **Estado:** Aceptado
- **Fecha:** 2026-11-11
- **Contexto:** Durante la auditoría de seguridad previa a la integración con el frontend (`NalunPos-Front`), se identificaron vulnerabilidades y acoplamientos críticos en la emisión del contrato de sesión y en la gestión de roles bajo Row-Level Security (RLS) en SQL Server 2022:
  1. `ITokenGenerator` conservaba un overload legado (`GenerateToken(User)`) que emitía el literal `"User"` y omitía `ClaimTypes.Role` para los roles operativos de tenant (`TenantAdmin`, `Supervisor`, `InventoryManager`, `Cajero`), mientras `LoginResponse` tampoco exponía el nombre canónico del rol.
  2. **Riesgo crítico P1 (escalación por colisión de nombre / `RoleId` insuficiente):** Un rol personalizado de tenant llamado `"SuperAdmin"` (con variaciones de mayúsculas o espacios) o una combinación inconsistente `SuperAdminRoleId` + `TenantId != null` podía inducir confusión entre roles de plataforma y de tenant.
  3. El aprovisionamiento de roles de sistema estaba fragmentado: `DefaultRoleSeeder` definía los 4 roles con sus permisos solo para tests, mientras `CreateTenantCommandHandler` creaba únicamente `TenantAdmin` y `Cajero` sin permisos.
  4. `AuthUserLookup` mezclaba consulta cross-tenant con un `try/catch (InvalidOperationException)` implícito para tolerar `InMemory`, duplicando SQL crudo y sin garantizar el cierre balanceado de conexión y la restauración previa del `SESSION_CONTEXT`.
  5. En HTTP, los callbacks de `HybridCache.GetOrCreateAsync` se ejecutan bajo `ExecutionContext.SuppressFlow()`, donde `IHttpContextAccessor.HttpContext` es `null`, requiriendo preservar la identidad del request sin efectos laterales en los getters ni snapshots mutables en `CurrentTenantContext`.

---

## Decisión

### 1. Nombres Canónicos de Roles, Reserva Estricta y Guardas Roslyn
- **Constantes únicas en Dominio:**
  - Plataforma: `PlatformRoles.SuperAdmin` (`"SuperAdmin"`).
  - Tenant: `TenantRoles.TenantAdmin` (`"TenantAdmin"`), `TenantRoles.Supervisor` (`"Supervisor"`), `TenantRoles.InventoryManager` (`"InventoryManager"`), `TenantRoles.Cajero` (`"Cajero"`).
- **Conjuntos congelados e insensibles a mayúsculas/espacios (`Pos.Domain.Common.RoleNames`):**
  - `RoleNames.TenantSystem` (`FrozenSet<string>` con `StringComparer.OrdinalIgnoreCase`) y `RoleNames.Reserved` (`PlatformRoles.SuperAdmin` + `TenantSystem`).
  - `RoleNames.IsReserved(string? name)` y `RoleNames.IsTenantSystemRole(string? name)` aplican `Trim()` previo con comparación `OrdinalIgnoreCase`.
- **Gobernanza sintáctica en `Pos.Architecture.Tests` (`RoleGovernanceTests` + `ReservedRoleLiteralScanner`):**
  - Prohibido usar literales reservados de rol en código de producción (`src/`) fuera de `PlatformRoles.cs` y `TenantRoles.cs`, con ratchet sellado permanentemente en `0`.

### 2. Aprovisionamiento Único y Factorías de Dominio en `Role`
- **Catálogo único (`Pos.Application.Common.Authorization.DefaultTenantRoleCatalog`):** Define `IReadOnlyList<TenantRoleDefinition> All` como única fuente de verdad (nombre canónico vía `TenantRoles`, descripción y permisos) consumida tanto por `CreateTenantCommandHandler` en producción como por `DefaultRoleSeeder` en pruebas/siembra idempotente.
- **Factorías de `Role` en Dominio:**
  - `Role.Create(...)`: exclusivo para roles personalizados del tenant; rechaza con `DomainException` cualquier nombre que satisfaga `RoleNames.IsReserved(...)`.
  - `Role.CreateSystemRole(...)`: exclusivo para los 4 roles de sistema del tenant; exige `TenantGuard.RequireTenantId(...)` y valida pertenencia a `RoleNames.TenantSystem`, normalizando al nombre canónico exacto.
  - `Role.CreatePlatformRole(...)`: exclusivo para `SuperAdmin`; exige `id == Role.SuperAdminRoleId`, `tenantId == PlatformScope.TenantId` y `name == PlatformRoles.SuperAdmin` (`StringComparison.Ordinal`).

### 3. Resolución Estricta de `SuperAdmin` (Cierre de P1 en Todas las Capas)
- Un usuario es **`SuperAdmin` de plataforma únicamente cuando se cumplen de manera simultánea**: `user.RoleId == Role.SuperAdminRoleId` **y** `user.TenantId == null`.
- **Defensa en profundidad capa por capa:**
  - **Dominio:** `User.ChangeRole` impide asignar `Role.SuperAdminRoleId` a cualquier usuario con `TenantId`; `Role.Create` bloquea nombres reservados.
  - **Aplicación:** `CreateRoleCommandValidator` (error code `RoleErrorCodes.NameReserved` = `"role.name_reserved"`) y `CreateRoleCommandHandler` rechazan crear roles con nombre reservado; `UpdateUserRoleCommandHandler` exige coincidencia exacta con `tenantId` del contexto y rechaza explícitamente `Role.SuperAdminRoleId`.
  - **Infraestructura (`AuthUserLookup`):** Si `user.RoleId == Role.SuperAdminRoleId || !user.TenantId.HasValue` fuera de la combinación válida `(SuperAdminRoleId, null)`, falla cerrado retornando `null` (HTTP `401`). Para usuarios de tenant, tras resolver el rol bajo `TenantId`, si el nombre de rol está vacío o equivale a `PlatformRoles.SuperAdmin` (`OrdinalIgnoreCase`), también falla cerrado retornando `null` (`401`).
  - **Infraestructura (`JwtTokenGenerator`):** Falla cerrado con `InvalidOperationException` ante cualquier combinación inconsistente entre `user.RoleId`, `roleName` y `user.TenantId`.

### 4. Contrato Estable HTTP y JWT (`ITokenGenerator`, `JwtTokenGenerator`, `LoginResponse`)
- **`ITokenGenerator` (`Pos.Application.Common.Interfaces`)** expone **un único método**:
  ```csharp
  string GenerateToken(User user, string roleName);
  ```
- **`JwtTokenGenerator`:** Rechaza `user == null` (`ArgumentNullException`) y `roleName` nulo, vacío o en blanco (`ArgumentException.ThrowIfNullOrWhiteSpace`). Emite exactamente estos claims (con expiración de 60 minutos vía `TimeProvider`):
  - `ClaimTypes.NameIdentifier` (`user.Id`)
  - `ClaimTypes.Email` (`user.Email.Value`)
  - `ClaimTypes.Name` (`user.FullName`)
  - `"roleId"` (`user.RoleId`)
  - `ClaimTypes.Role` (nombre canónico normalizado)
  - `"tenantId"` (`user.TenantId`, **únicamente** para usuarios de tenant; **jamás** emitido para `SuperAdmin`).
- **`LoginResponse`:**
  ```csharp
  public sealed record LoginResponse(
      string Token,
      Guid UserId,
      Guid RoleId,
      string Role,
      string Email,
      string FullName,
      Guid? TenantId,
      DateTime ExpiresAtUtc);
  ```

### 5. Sesión de Autenticación (`IAuthenticationSessionScope`) y Contexto HTTP sin Efectos Laterales
- **Autenticación bajo RLS (`IAuthenticationSessionScope` / `SqlServerAuthenticationSessionScope`):**
  - Cero `try/catch (InvalidOperationException)` ni condicionales de proveedor en `src/Pos.Infrastructure/Authentication/` (protegido por `AuthenticationGovernanceTests` con ratchet en `0`).
  - `SqlServerAuthenticationSessionScope` registra el estado inicial de la conexión y los valores previos de `SESSION_CONTEXT('TenantId')` y `SESSION_CONTEXT('IsSuperAdmin')`:
    1. Activa `AllowGlobalUserLookup = 1` solo durante la consulta de `Users` por email y lo desactiva inmediatamente en bloque `finally`.
    2. Para usuarios de tenant, fija `AllowGlobalUserLookup = 0`, `IsSuperAdmin = 0` y `TenantId = user.TenantId` mediante `SetTenantScopeAsync` para resolver el `Role` bajo RLS real de SQL Server.
    3. Al disponer (`DisposeAsync`), restaura los valores previos de `SESSION_CONTEXT` (forzando `AllowGlobalUserLookup = 0`) y cierra la conexión solo si estaba cerrada al inicio.
- **Resolución HTTP explícita y getters puros (`ExecutionContext.SuppressFlow()` de `HybridCache`):**
  - `TenantClaimsResolver` (`Pos.Infrastructure.Multitenancy`) constituye la fuente única de parseo de `HttpContext.Items["TenantId"]` y claims (`tenantId`, `tenant_id`, `tid`), devolviendo `(Guid? TenantId, bool IsSuperAdmin)` donde `IsSuperAdmin = user.IsInRole(PlatformRoles.SuperAdmin) && TenantId is null`.
  - `TenantResolutionMiddleware` valida `context.User.Identity?.IsAuthenticated == true`, resuelve con `TenantClaimsResolver` y fija el estado explícitamente en el scope DI mediante `ITenantSetter.SetTenantId` / `SetSuperAdmin` (y `context.Items["TenantId"]` si hay tenant). Los requests anónimos no reciben estado manual (`TenantId = null`, `IsSuperAdmin = false`).
  - `CurrentTenantContext` expone getters **puros y sin efectos laterales** (`_hasManualTenantId` desambigua `SetTenantId(null)` de contexto sin fijar). Al haberse fijado en el scope DI antes de la ejecución de handlers/caché, los callbacks de `HybridCache.GetOrCreateAsync` bajo `ExecutionContext.SuppressFlow()` (donde `HttpContext == null`) preservan el contexto correcto para `PosDbContext` y `TenantSessionContextInterceptor`.

### 6. Convención de `ValidationResultFactory` para Códigos Específicos
- En `Pos.Application.Common.Validation.ValidationResultFactory.CreateError`:
  - Cuando existe **exactamente un fallo** (`failureList.Count == 1`) con un único `ErrorCode` personalizado que contiene punto (`'.'`, p. ej. `RoleErrorCodes.NameReserved = "role.name_reserved"`), el `DomainError.Code` resultante promueve ese código específico y `PosProblemDetailsFactory` lo serializa en `extensions["code"]` del `ProblemDetails` RFC 9457 (además del detalle en `errors`).
  - En validaciones múltiples o genéricas se mantiene `Validation.Error`.

### 7. Contrato Estricto hacia `NalunPos-Front` (Fail-Closed)
- `NalunPos-Front` debe basar el acceso y los privilegios de UI exclusivamente en el campo canónico `Role` de `LoginResponse` y/o el claim `role` (`ClaimTypes.Role`) del JWT validado en servidor:
  - **Roles canónicos reconocidos:** `SuperAdmin`, `TenantAdmin`, `Supervisor`, `InventoryManager`, `Cajero`.
  - **Política Fail-Closed:** Si el rol está ausente, vacío, nulo o no coincide con uno de los nombres canónicos esperados para el módulo/vista, el frontend debe otorgar **cero privilegios** y denegar el acceso al módulo.

---

## Consecuencias

- **Positivas:**
  - Eliminación total de roles ambiguos (`"User"`), colisiones de nombres reservados y vectores de escalación hacia `SuperAdmin`.
  - Contrato único de emisión JWT (`ITokenGenerator.GenerateToken(User, string)`) verificado en pruebas unitarias e integración HTTP real contra SQL Server 2022 con RLS.
  - Desacoplamiento limpio entre obtención de identidad (`TenantResolutionMiddleware` + `TenantClaimsResolver`), fijación explícita por scope (`ITenantSetter`) y lectura pura sin efectos laterales (`CurrentTenantContext`), compatible con `HybridCache` y background workers.
- **Negativas / Trade-offs aceptados:**
  - `TenantResolutionMiddleware` pasa a formar parte de la lista blanca explícita de `SetSuperAdmin` en `Pos.Architecture.Tests` junto a `OutboxProcessorBackgroundService` y `PendingInvoicePlatformReader`, al ser el único punto HTTP autorizado para propagar identidad de plataforma ya validada desde el JWT.
  - Los roles personalizados creados por tenants distintos a los 4 canónicos (`TenantAdmin`, `Supervisor`, `InventoryManager`, `Cajero`) recibirán en `LoginResponse.Role` su nombre propio; el frontend aplicará política *fail-closed* si espera un rol canónico de sistema.

