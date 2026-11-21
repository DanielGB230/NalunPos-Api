# ADR 0032: Política de Limpieza de la Cookie de Refresh

- **Estado:** Aceptado
- **Fecha:** 2026-11-21
- **Contexto:** Cierra el comportamiento descrito en el ADR 0030 (línea 22) y precisa el alcance del ADR 0031 (línea 45). `ToAuthResultAsync` es compartido por login y refresh y borraba la cookie `nalunpos_rt` en **cualquier** error salvo `Auth.RefreshConcurrent`.
- **Alcance:** Decisión de cuándo se elimina la cookie HttpOnly de refresh. No incluye revocación de sesiones por eventos de seguridad, vigencia absoluta de la familia ni purga de tokens.

## Problema

`ToAuthResultAsync` borraba la cookie ante cualquier error. Sin embargo, varios errores no consumen el refresh token y son recuperables:

- **403 por estado de tenant** (`Tenant.Suspended`, `Tenant.NotActive`): el refresh no consume el token; al reactivar el tenant, el mismo token vuelve a funcionar.
- **401 `Auth.UserInvalid`** (usuario inactivo): el refresh no consume el token; al reactivar el usuario, el mismo token vuelve a funcionar.
- **409 `Auth.RefreshConcurrent`**: el perdedor de una carrera no debe borrar la cookie nueva del ganador (ADR 0031).
- **Fallos de login** (401 `Auth.InvalidCredentials`, 403 de tenant, 400 de validación): un intento fallido en una pestaña borraba la cookie de una sesión válida en otra.

En todos esos casos el navegador perdía el token aunque la sesión siguiera siendo restaurable, obligando a un login manual innecesario.

## Decisión

1. **Una sola fuente de la regla:** `RefreshTokenCookiePolicy.ShouldClear(DomainError)` en `src/Pos.Api/Extensions` devuelve `true` únicamente cuando el código es `Auth.RefreshTokenInvalid` (comparación `Ordinal`, sin literales: referencia `RefreshTokenErrors.InvalidCode` de Application).
2. **`ToAuthResultAsync`** consulta la política antes de limpiar la cookie. Se elimina el caso especial del 409, que queda cubierto por la regla general (no es `Auth.RefreshTokenInvalid`). No cambia ningún otro comportamiento.
3. **Logout no cambia:** sigue borrando la cookie siempre, de forma idempotente, con independencia del resultado del comando.
4. **OpenAPI no cambia:** no se modifican los `[ProducesResponseType]` ni el contrato HTTP.

Consecuencia directa: la cookie solo se elimina cuando la sesión murió definitivamente (token inexistente, expirado, revocado por logout o por reuso fuera de la ventana de tolerancia).

## Alternativas descartadas

1. **Borrar siempre (comportamiento anterior):** pierde sesiones recuperables. Un tenant suspendido o un usuario desactivado temporalmente obligan a reautenticar aunque el token siga siendo válido tras la reactivación. Un login fallido en una pestaña cierra la sesión de otra.
2. **Borrar con cualquier 401:** `Auth.UserInvalid` también es recuperable (usuario inactivo). Tratarlo como terminal confunde "sesión no restaurable ahora" con "sesión muerta" y fuerza un login manual evitable.
3. **Borrar en el front según el código HTTP:** duplica la regla en dos capas y deja la decisión fuera del servidor, que es quien conoce si el token se consumió. Descartada: una sola fuente en la API.

## Análisis de seguridad

- La cookie es **HttpOnly** (no accesible desde JavaScript), **SameSite=Strict** (no se envía en peticiones cross-site) y está acotada a **`Path=/api/v1/auth`** (solo viaja a login/refresh/logout). Conservarla no amplía la superficie de exposición.
- Cada refresh **revalida de forma fail-closed**: existencia y estado del usuario, rol vigente y estado del tenant. Mientras el usuario esté inactivo, el rol haya cambiado o el tenant esté suspendido, el refresh responde 401/403 y **no emite tokens**; la cookie por sí sola no concede acceso.
- El token sigue almacenado solo como hash SHA-256 en BD (ADR 0030). Conservar la cookie en el navegador no altera esa invariante.
- Un atacante con acceso al navegador ya podría usar la cookie antes del error; no borrarla no le otorga ninguna capacidad nueva.
- El 409 no emite cookie ni tokens (ADR 0031); el perdedor no gana nada conservando la suya, que pertenece al ganador.

## Consecuencias verificables

- **Las sesiones NO se revocan** al desactivar un usuario, cambiar su rol o suspender el tenant.
- Mientras dure ese estado, el refresh **bloquea** la emisión de tokens (401 `Auth.UserInvalid`, 403 de tenant) **sin borrar la cookie**.
- Al reactivar el usuario o el tenant, la **reactivación reanuda** la sesión con el mismo token dentro de su vigencia.
- Un login fallido no afecta a una sesión válida en otra pestaña.
- `Auth.RefreshTokenInvalid` sigue limpiando la cookie: token inexistente, expirado, revocado por logout o reuso fuera de la ventana.

## Contrato para el frontend

1. Tras **401 `Auth.UserInvalid`** o **403** de estado de tenant: **no** reintentar refresh en bucle. Tratarlo como **"sesión no restaurable ahora"**: conservar la cookie, detener los reintentos y permitir el **login manual**.
2. Tras **409 `Auth.RefreshConcurrent`**: aplicar el ADR 0031 (single-flight/Web Locks y, si llega, reintento acotado usando la cookie vigente).
3. Tras **401 `Auth.RefreshTokenInvalid`**: la sesión murió; limpiar el estado local y redirigir al login (la cookie ya la borró el servidor).

## Referencias

- [ADR 0030](0030-refresh-tokens-rotativos-cookie-httponly.md): refresh tokens rotativos y cookie HttpOnly.
- [ADR 0031](0031-ventana-tolerancia-rotacion-refresh-token.md): ventana de tolerancia y 409 `Auth.RefreshConcurrent`.
