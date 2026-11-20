# ADR 0030: Refresh Tokens Rotativos con Cookie HttpOnly y Detección de Reuso por Familia

- **Estado:** Aceptado
- **Fecha:** 2026-11-19
- **Contexto:** Implementación de F1.G para completar el sistema de autenticación con refresh tokens rotativos, cookie HttpOnly `nalunpos_rt`, endpoints `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me`, y migración del modelo de datos.

## Decisión

### 1. Refresh Token Rotativo (Rotating Refresh Tokens)
- **Token opaco de 256 bits** generado con `RandomNumberGenerator` (claro, solo para transporte en cookie).
- **Solo hash SHA-256** se persiste en BD (`RefreshTokens.TokenHash`, `CHAR(64)`).
- **Familia de tokens** (`FamilyId`): cada login crea una nueva familia; cada refresh rota el token dentro de la misma familia.
- **Revocación atómica**: el token actual se revoca y el nuevo se inserta en la misma transacción (`ExecuteUpdateAsync` condicional + `INSERT`).
- **Detección de reuso**: si un token ya revocado se presenta de nuevo → se revoca **toda la familia** (`ReuseDetected`) y se retorna 401. La rotación concurrente dentro de la ventana de tolerancia se rige por el ADR 0031 (409 `Auth.RefreshConcurrent`, sin revocar la familia).
- **Vigencia configurable** (`RefreshTokenDays`, default 7) vía `RefreshTokenOptions` con `ValidateDataAnnotations().ValidateOnStart()`.

### 2. Cookie HttpOnly `nalunpos_rt`
- **Nombre**: `nalunpos_rt` (configurable).
- **Atributos**: `HttpOnly`, `SameSite=Strict`, `Path=/api/v1/auth`.
- **Secure**: `true` fuera de Development; `SameAsRequest` en Development.
- **Max-Age**: igual a la vigencia del refresh token en segundos.
- **Limpieza en error**: en cualquier 401 de refresh/login, la cookie se borra (`Max-Age=0`).
- **Logout**: borra la cookie de forma idempotente (204 aunque no exista).

### 3. Endpoints
| Endpoint | Auth | Descripción |
|---|---|---|
| `POST /api/v1/auth/login` | `AllowAnonymous` + RateLimit `AuthPolicy` | Credenciales → 200 `LoginResponse` + cookie `nalunpos_rt` |
| `POST /api/v1/auth/refresh` | `AllowAnonymous` + RateLimit `AuthPolicy` | Lee cookie → **valida usuario/rol/tenant fail-closed** → rota token → 200 `LoginResponse` + nueva cookie; 403/401 + limpieza cookie si usuario inactivo, tenant suspendido/inexistente; 401 + revocación familia si reuso; 401 si token inválido/expirado |
| `POST /api/v1/auth/logout` | `AllowAnonymous` + RateLimit `AuthPolicy` | Revoca token de cookie (si existe) → borra cookie → 204 idempotente |
| `GET /api/v1/auth/me` | `Authorize` | Resuelve identidad desde claims JWT → 200 `CurrentUserResponse` (`userId`, `roleId`, `role`, `email`, `fullName`, `tenantId`); 401 si claims incompletos/inconsistentes (fail-closed ADR 0023) |

### 4. Modelo de Datos (`RefreshTokens`)
```sql
CREATE TABLE RefreshTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    TokenHash CHAR(64) NOT NULL UNIQUE,
    FamilyId UNIQUEIDENTIFIER NOT NULL,
    ReplacedByTokenHash CHAR(64) NULL,
    RevokedReason NVARCHAR(100) NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    ExpiresAtUtc DATETIME2 NOT NULL,
    RevokedAtUtc DATETIME2 NULL
);
-- Índices:
-- IX_RefreshTokens_TokenHash (UNIQUE)
-- IX_RefreshTokens_UserId_FamilyId_Active (UNIQUE WHERE RevokedAtUtc IS NULL)
-- IX_RefreshTokens_ExpiresAtUtc
-- IX_RefreshTokens_UserId
```
- **No implementa `ITenantOwnedEntity`**: el aislamiento es por `UserId` y `FamilyId`.
- **No está bajo RLS**: la tabla `RefreshTokens` no tiene `TenantId` ni está cubierta por la `TenantSecurityPolicy`. El acceso se gobierna por hash de token (`TokenHash` único) y `FamilyId` bajo scope de autenticación.
- **Convención UTC**: todas las columnas `*Utc` con `Kind=Utc` vía `UtcDateTimeConvention`.

### 5. Capa Application (Puertos y CQRS)
- **Puertos** en `Pos.Application.Common.Interfaces`:
  - `IRefreshTokenGenerator` → `RefreshTokenMaterial(Token, TokenHash)`
  - `IRefreshTokenStore` → `FindByHashAsync`, `IssueAsync`, `RevokeAsync`, `RevokeFamilyAsync`, `RotateAsync`
  - `ICurrentUserClaims` / `CurrentUserClaims` → lectura de claims desde `HttpContext.User`
- **CQRS** (un archivo por comando/query, validador propio, `[AllowsNoTenant][PublicUseCase]`):
  - `RefreshTokenCommand` / `RefreshTokenCommandHandler` → **busca token, valida reuso/expiración, valida usuario/rol/tenant fail-closed (ADR 0023), SOLO DESPUÉS rota** el token
  - `LogoutCommand` / `LogoutCommandHandler` → revoca token, idempotente
  - `GetCurrentUserQuery` / `GetCurrentUserQueryHandler` → resuelve identidad desde claims (sin BD)
- **LoginCommandHandler** actualizado: emite y persiste refresh token inicial (`IssueAsync`).

### 6. Capa Infrastructure
- **`RefreshTokenGenerator`**: genera token opaco + hash SHA-256.
- **`RefreshTokenStore`**: usa `IAuthenticationSessionScope` para todas las operaciones (acceso por `TokenHash` y `FamilyId`); rotación atómica con transacción + `ExecuteUpdateAsync` condicional.
- **`RefreshTokenConfiguration`**: Fluent API con índices únicos y filtro `RevokedAtUtc IS NULL`.
- **`HttpContextCurrentUserClaims`**: implementa `ICurrentUserClaims` leyendo claims estándar (`NameIdentifier`, `roleId`, `Role`, `Email`, `Name`, `tenantId`).
- **`AuthUserLookup.FindByIdAsync`**: nuevo método para re-validar usuario/rol en refresh (misma lógica fail-closed que `FindByEmailAsync`).
- **DI**: registro de `IRefreshTokenGenerator`, `IRefreshTokenStore`, `ICurrentUserClaims`, `RefreshTokenOptions` (singleton POCO para inyección directa en handlers).

### 7. Capa API
- **`RefreshTokenCookieWriter`**: escribe/borra cookie con atributos correctos.
- **`ResultExtensions.ToAuthResultAsync`**: usa `IProblemDetailsService.WriteAsync` para garantizar `application/problem+json` en errores.
- **`AuthController`**: endpoints ultra-delgados, delegan en `IDispatcher`, no acceden a `result.Value`.
- **CORS**: `AllowCredentials()` solo con `CorsSettings:AllowedOrigins` explícitos (ya existente).

### 8. OpenAPI y Migración
- **Migración `AddRefreshTokens`**: `has-pending-model-changes` limpio.
- **Snapshot OpenAPI actualizado**: nuevos paths `/auth/refresh`, `/auth/logout`, `/auth/me` y schema `CurrentUserResponse`.

### 9. Acceso a RefreshTokens bajo `IAuthenticationSessionScope`
- `RefreshTokenStore` usa `IAuthenticationSessionScope` (patrón `SqlServerAuthenticationSessionScope`) para elevar temporalmente `AllowGlobalUserLookup` y consultar `RefreshTokens` **sin filtro de tenant** (la tabla no tiene RLS ni `TenantId`).
- **No usa `IgnoreQueryFilters()` suelto**: el scope maneja `SESSION_CONTEXT` (`AllowGlobalUserLookup`, `IsSuperAdmin`, `TenantId`) de forma balanceada y segura.

## Justificación
- **Seguridad**: Rotación + detección de reuso mitiga robo de refresh token (RFC 6749, OWASP).
- **HttpOnly + SameSite=Strict + Path acotado**: defensa en profundidad contra XSS/CSRF.
- **Fail-closed**: re-validación de usuario/rol en cada refresh (ADR 0023).
- **Scope de autenticación consistente**: `RefreshTokenStore` usa el mismo patrón de scope que `AuthUserLookup`, con acceso gobernado por `TokenHash` y `FamilyId`.
- **Sin BD en `/me`**: identidad resuelta 100% desde claims JWT.
- **Logout idempotente**: UX robusta, sin errores si no hay cookie.

## Consecuencias
- **Nueva tabla `RefreshTokens`** con índices únicos compuestos.
- **Nueva configuración `RefreshToken`** (validada al arranque).
- **Cookies** requieren HTTPS en producción (`Secure=true`).
- **Tests de arquitectura** validan validadores, atributos, y ausencia de `result.Value` en controladores.

## Deuda Técnica Explícita
1. ~~**Ventana de tolerancia para refresh concurrentes / reuso detectado**: si dos peticiones concurrentes usan el mismo refresh token válido, una ganará la rotación y la otra detectará reuso (`ReuseDetected`) revocando **toda la familia** (`FamilyId`) y cerrando todas las sesiones del usuario en ese login. No hay reintento automático; el cliente debe volver a loguearse.~~ **RESUELTA** en el ADR 0031 (ventana de tolerancia `RefreshToken:RotationLeewaySeconds`; el perdedor recibe 409 `Auth.RefreshConcurrent` sin revocar la familia).
2. **Vigencia absoluta de la familia**: no hay expiración máxima de la `FamilyId` independientemente de la rotación; una familia puede vivir indefinidamente mientras se rotan tokens. Futura mejora: añadir `FamilyExpiresAtUtc` y purgar familias expiradas.
3. **Purga de tokens expirados/revocados**: no hay job de limpieza automática de `RefreshTokens` antiguos; la tabla crece indefinidamente. Futura mejora: job periódico que elimine tokens con `RevokedAtUtc` o `ExpiresAtUtc` anterior a umbral configurable.

## Alternativas Consideradas
1. **Refresh token sin rotación**: rechazado por riesgo de robo prolongado.
2. **Refresh token en BD sin familia**: rechazado; sin familia no se detecta reuso.
3. **Refresh token en header Authorization**: rechazado; cookie HttpOnly es más seguro contra XSS.
4. **`/me` consultando BD**: rechazado; claims JWT ya tienen la info (ADR 0029 vigencia única).

## Referencias
- ADR 0001 (Clean Architecture), ADR 0008 (Multi-tenancy), ADR 0018 (TimeProvider), ADR 0023 (Roles/Session), ADR 0026 (UTC), ADR 0029 (Access Token Lifetime).
- RFC 6749 (OAuth 2.0), RFC 9457 (ProblemDetails), RFC 6265 (Cookies).