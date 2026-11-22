# ADR 0034: Revocación de Sesiones al Cambiar el Estado del Usuario

- **Estado:** Aceptado
- **Fecha:** 2026-11-22
- **Contexto:** Cierra el hueco de seguridad descrito en el ADR 0032 (línea 44): desactivar un usuario no invalidaba sus refresh tokens; el refresh los bloqueaba mientras el usuario estaba inactivo, pero se reanudaban al reactivarlo. La autenticación ya revalida usuario y rol por petición, por lo que el hueco era la persistencia de las sesiones, no el control de acceso.
- **Alcance:** Revocación de las sesiones (refresh tokens) de un usuario cuando cambia su estado activo/inactivo por `PATCH /api/v1/users/{id}/status`. No incluye revocación por cambio de rol, por suspensión de tenant, cambio/reseteo de contraseña, kill switch global ni offboarding terminal del tenant.

## Problema

`DeactivateUserCommandHandler` y `ActivateUserCommandHandler` cambiaban `User.IsActive` y guardaban, sin tocar `RefreshTokens`. Mientras el usuario estaba inactivo, el refresh devolvía 401 `Auth.UserInvalid` sin consumir el token y sin borrar la cookie (ADR 0032). Al reactivarlo, el **mismo** token volvía a emitir access tokens: una sesión que debía haber terminado al desactivar al usuario se reanudaba.

Los access tokens de un usuario inactivo ya no permiten ejecutar casos de uso protegidos porque la autorización se reevalúa por petición, pero eso no convierte en inocua la reanudación del refresh: el token de refresh sobrevivía al evento de seguridad.

## Decisión

1. **Dos motivos canónicos nuevos** en `RefreshTokenRevocationReasons`: `UserDeactivated` y `UserReactivated` (`src/Pos.Domain/Authentication/RefreshTokenRevocationReasons.cs`).
2. **Puerto de Application** `IUserSessionRevoker.RevokeAllAsync(Guid userId, string reason, CancellationToken) → int` en `src/Pos.Application/Common/Interfaces/IUserSessionRevoker.cs`. Devuelve la cantidad de tokens activos revocados.
3. **Implementación propia** `UserSessionRevoker` en `src/Pos.Infrastructure/Authentication/UserSessionRevoker.cs`, registrada en el DI de Infrastructure. Ejecuta un único `ExecuteUpdateAsync` sobre `RefreshTokens` con filtro `UserId == @u AND RevokedAtUtc IS NULL`, fijando `RevokedAtUtc` desde `TimeProvider` en UTC y `RevokedReason` con el motivo recibido.
   - **Idempotente:** un segundo intento no altera los tokens ya revocados y devuelve `0`.
   - **Sin `IAuthenticationSessionScope` y sin búsqueda global de usuarios:** `RefreshTokens` no está bajo RLS (la `SECURITY POLICY` de `InitialCreate` no la incluye), por lo que no se amplía el alcance de RLS dentro de una petición de tenant.
   - No se modifica `RefreshTokenStore`.
4. **Desactivar:** `DeactivateUserCommandHandler` guarda el estado y, tras un `SaveChangesAsync` exitoso, revoca todas las sesiones del usuario con `UserDeactivated`. No se invoca si el usuario no existe o ya está inactivo.
5. **Reactivar:** `ActivateUserCommandHandler` revoca **antes** de `Activate`/`SaveChangesAsync` con `UserReactivated`, y solo después activa y guarda. No se invoca si el usuario no existe o ya está activo. Si la revocación lanza, no se guarda la activación.
6. **Cambiar el rol no revoca** (`UpdateUserRoleCommandHandler` no depende del revocador). **Suspender el tenant no revoca** (decisión vigente del ADR 0032).
7. **Sin cambios en el handler de refresh:** un token revocado con `UserDeactivated`/`UserReactivated` entra en la ruta existente de motivo distinto de `Rotated` → reuso → familia ya revocada → 401 `Auth.RefreshTokenInvalid` y limpieza de cookie.

### Por qué se revoca también al activar

La desactivación revoca **después** de guardar: si la revocación fallara, el usuario queda inactivo con tokens activos. Reintentar la desactivación devolvería `User.AlreadyInactive` y no volvería a intentar la revocación. Revocar al activar, antes de guardar, cierra ese hueco: cualquier token que sobreviviera a una revocación fallida se elimina antes de devolver al usuario al estado activo; si la revocación falla, la activación no se persiste y el usuario sigue inactivo (el refresh continúa bloqueado con `Auth.UserInvalid`).

La asimetría es deliberada:

- Desactivar revoca después de guardar. Si se revocara antes y el guardado fallara, un usuario activo perdería todas sus sesiones sin motivo.
- Activar revoca antes de guardar. Si se guardara primero y la revocación fallara, el usuario quedaría activo con tokens que debieron morir en la desactivación, sin un reintento natural.

## Evidencia de que el estado y el rol se reevalúan por petición

- **Access tokens de usuarios inactivos rechazados:** `AuthorizationBehavior<TRequest, TResponse>` (`src/Pos.Application/Common/Behaviors/AuthorizationBehavior.cs`, líneas 71-88) carga el usuario desde BD en **cada** comando/consulta protegida, rechaza `user is null || !user.IsActive` con 401 y evalúa los permisos con el `user.RoleId` leído de BD. Un access token emitido antes de la desactivación no permite ejecutar casos de uso protegidos.
- **El rol se evalúa desde BD:** el mismo behavior resuelve permisos con `user.TenantId` y `user.RoleId` vigentes (líneas 77-88), no con el rol congelado en el JWT.
- **El refresh recalcula el rol:** `RefreshTokenCommandHandler` (`src/Pos.Application/Authentication/Commands/RefreshToken/RefreshTokenCommandHandler.cs`, líneas 80-120) revalida existencia/estado del usuario, obtiene el rol actual con `IAuthUserLookup.FindByIdAsync` y emite el nuevo access token con `authUser.Role` vigente. Un cambio de rol surte efecto en el siguiente refresh sin revocar la sesión.
- **`/auth/me` desde claims:** `GetCurrentUserQueryHandler` (`src/Pos.Application/Authentication/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs`) resuelve la identidad solo con `ICurrentUserClaims`, implementado por `HttpContextCurrentUserClaims` (`src/Pos.Infrastructure/Authentication/HttpContextCurrentUserClaims.cs`) leyendo el JWT. No consulta BD (ADR 0030, línea 101).

## Por qué NO se revoca por cambio de rol

El rol no está congelado en la sesión: se lee de BD en cada petición protegida y el refresh lo recalcula. Revocar por cambio de rol obligaría a reautenticar sin ganar seguridad, porque el privilegio nuevo ya está vigente y el anterior ya no se aplica. La revocación se reserva para eventos que invalidan la **credencial/sesión** (estado del usuario, y en el futuro contraseña), no para cambios de autorización que ya son inmediatos.

## Por qué NO se revoca por suspensión del tenant

Suspender un tenant es un estado reversible que el ADR 0032 trata como "sesión no restaurable ahora": el refresh responde 403 sin consumir el token ni borrar la cookie, y al reactivar el tenant la sesión se reanuda. Revocar masivamente las sesiones de todos los usuarios del tenant excede este paso, duplicaría la política del ADR 0032 y requiere su propio diseño de recuperación. El offboarding **terminal** del tenant sí necesitará revocación masiva y se diseñará cuando exista ese estado.

## Alternativas descartadas

1. **Outbox / evento de integración con consumidor:** la revocación sería eventual y añadiría evento, contrato, consumidor, reintentos, idempotencia y observabilidad para una actualización que el propio handler puede ejecutar en la misma transacción lógica y en el mismo proceso. Además, el evento se publica al guardar, dejando una ventana en la que el refresh seguiría vivo.
2. **Revocar antes de guardar al desactivar:** si la revocación tuviera éxito y el guardado fallara, un usuario que sigue activo perdería todas sus sesiones. El orden elegido (guardar y después revocar) evita ese efecto y se apoya en que, si la revocación falla, el refresh queda bloqueado por `Auth.UserInvalid` y la reactivación cierra el hueco.
3. **Revocar desde `RefreshTokenStore`:** mezclaría la responsabilidad de rotación por hash/familia con la revocación masiva por usuario y obligaría a abrir el alcance global de usuarios. Se crea un colaborador propio.
4. **Revocar por rol o por tenant en este paso:** cubierto en las secciones anteriores; no son eventos de credencial en el alcance actual.

## Límite conocido por diseño

`GET /api/v1/auth/me` es `[PublicUseCase]` y `[Authorize]`: la autenticación valida la firma y vigencia del JWT, pero la identidad se resuelve **desde los claims** sin consultar BD (ADR 0030). Por tanto, un access token emitido antes de desactivar al usuario puede seguir mostrando su identidad en `/auth/me` hasta que expire. No concede permisos ni acceso a datos: cualquier caso de uso protegido vuelve a cargar el usuario y lo rechaza si está inactivo. Cualquier endpoint futuro que sea `[PublicUseCase]` y autenticado debe asumir esta misma limitación y no exponer capacidades.

## Regla para futuras funciones

- **Todo evento de credencial debe revocar las sesiones del usuario:** cambio de contraseña y reseteo de contraseña usarán el mismo puerto `IUserSessionRevoker` y un motivo canónico propio.
- **Offboarding terminal del tenant:** cuando exista el estado terminal, su diseño incluirá la revocación masiva de las sesiones de todos los usuarios del tenant, reutilizando el patrón de revocación en bloque.
- Cualquier nueva transición de estado del usuario que invalide la sesión deberá declarar explícitamente si revoca y con qué motivo.

## Consecuencias verificables

- Desactivar un usuario revoca **todas** sus familias de refresh tokens activas en una sola sentencia; el refresh posterior devuelve 401 `Auth.RefreshTokenInvalid` y limpia la cookie (ADR 0032).
- Reactivar un usuario **no reanuda** el token revocado al desactivarlo.
- Reactivar revoca cualquier token activo que hubiera sobrevivido a una revocación fallida, con motivo `UserReactivated`.
- El aislamiento se mantiene: el filtro es por `UserId`; las sesiones de otros usuarios del mismo tenant y de otros tenants siguen refrescando con 200.
- Cambiar el rol no revoca: el refresh sigue en 200 y el access token nuevo lleva el rol nuevo.
- La revocación es idempotente y no requiere `SaveChangesAsync` (el `ExecuteUpdateAsync` persiste directamente).
- No cambia el contrato OpenAPI, no se añaden paquetes y no se modifica el frontend.

## Referencias

- [ADR 0030](0030-refresh-tokens-rotativos-cookie-httponly.md): refresh tokens rotativos, cookie HttpOnly e identidad de `/auth/me` desde claims.
- [ADR 0031](0031-ventana-tolerancia-rotacion-refresh-token.md): ventana de tolerancia y clasificación de tokens revocados.
- [ADR 0032](0032-politica-limpieza-cookie-refresh.md): política de limpieza de la cookie de refresh.
- [ADR 0033](0033-vigencia-absoluta-y-purga-refresh-tokens.md): vigencia absoluta de la familia y purga de refresh tokens.
