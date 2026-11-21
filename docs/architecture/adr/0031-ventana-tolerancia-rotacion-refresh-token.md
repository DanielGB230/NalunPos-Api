# ADR 0031: Ventana de Tolerancia en la Rotación de Refresh Tokens

- **Estado:** Aceptado
- **Fecha:** 2026-11-20
- **Contexto:** Cierra la deuda técnica #1 del ADR 0030. Dos refresh simultáneos con el mismo token (dos pestañas, reintento de red o doble disparo de un interceptor) provocaban que el perdedor de la carrera revocara **toda** la familia y cerrara la sesión del usuario, aunque ambas peticiones fueran legítimas.
- **Alcance:** Rotación de refresh tokens (F1.G.5). No incluye vigencia absoluta de la familia ni purga (deudas #2 y #3 del ADR 0030, siguen abiertas).

## Problema

El handler revocaba la familia ante **cualquier** token ya revocado, por dos rutas:

1. `FindByHashAsync` devuelve el token ya revocado (el ganador rotó antes de la lectura).
2. `RotateAsync` pierde la carrera: la actualización condicional `WHERE RevokedAtUtc IS NULL` afecta 0 filas.

Además, `ToAuthResultAsync` borraba la cookie `nalunpos_rt` en cualquier error, de modo que el perdedor borraba del navegador la cookie nueva del ganador.

## Decisión

### 1. Política pura de dominio (`RefreshTokenReusePolicy`)

`RefreshTokenReusePolicy.Classify(revokedReason, revokedAtUtc, nowUtc, rotationLeeway)` clasifica un token revocado:

- **`ConcurrentRotation`** solo si `RevokedReason == "Rotated"`, `RevokedAtUtc` no es nulo y `(nowUtc − RevokedAtUtc) <= leeway` (límite inclusivo). Una diferencia negativa por desfase de reloj también está dentro de la ventana.
- **`Reuse`** en cualquier otro caso: motivo distinto de `Rotated` (`Logout`, `ReuseDetected`, `Expired`), `RevokedAtUtc` nulo, o fuera de la ventana.
- Leeway `0` desactiva la tolerancia.
- Valida que `nowUtc` y `revokedAtUtc` sean UTC (`DomainException` si no) y no consulta ningún reloj: el instante entra por parámetro (`TimeProvider` en el handler, ADR 0018/0026).

### 2. Contrato del store

- `RefreshTokenCandidate` incorpora `RevokedReason`.
- `RotateAsync` deja de devolver `ReuseDetected`: cuando `Success = false` retorna el estado actual del token en `RefreshTokenRotationOutcome.CurrentToken` (o `null` si no existe), leído dentro del mismo `IAuthenticationSessionScope`.

### 3. Handler: una sola política para las dos rutas

Un único método privado aplica la clasificación al estado de un token revocado, tanto en la lectura previa como en la carrera perdida:

- `ConcurrentRotation` → `409 Conflict` con código `Auth.RefreshConcurrent`; **no** revoca la familia y **no** emite tokens.
- `Reuse` → `RevokeFamilyAsync(ReuseDetected)` + `401 Auth.RefreshTokenInvalid` (comportamiento previo).
- Token inexistente → `401` sin revocar familia (sin cambios).

Los códigos viven en `RefreshTokenErrors` (Application) para que la capa API no use literales.

### 4. API

- `ToAuthResultAsync` **no** borra la cookie cuando el código es `Auth.RefreshConcurrent`; la política de limpieza completa se define en el ADR 0032 (solo `Auth.RefreshTokenInvalid` limpia la cookie).
- `POST /api/v1/auth/refresh` documenta `409` (`ProblemDetails`) en OpenAPI.

### 5. Configuración

| Clave | Defecto | Rango | Semántica |
|---|---|---|---|
| `RefreshToken:RotationLeewaySeconds` | `10` | `[0, 60]` | Segundos de tolerancia; `0` desactiva la ventana |

Se valida al arranque con `ValidateDataAnnotations()` + `ValidateOnStart()` (ADR 0018). No se modifica `appsettings` porque el valor por defecto cubre el caso normal.

## Alternativas descartadas

1. **Sin ventana (comportamiento anterior):** cualquier carrera legítima revoca la familia y obliga a reautenticar. Es el problema que motiva este ADR.
2. **Reemitir el token tolerado, como Auth0:** exige conservar el token en claro (o su equivalente recuperable) para devolverlo al perdedor. Viola la invariante de "solo hash en BD" del ADR 0030 y amplía la superficie de fuga. Descartada.
3. **Reintento automático en el servidor:** mantiene transacciones abiertas y no elimina la carrera; el cliente es quien conoce el estado de su cookie.

## Análisis de seguridad

- Un replay dentro de la ventana **no obtiene tokens**: recibe `409` sin `Set-Cookie`. Solo consigue retrasar la detección del reuso hasta `N` segundos (máximo 60). Pasada la ventana, el reuso sigue revocando toda la familia.
- El atacante que reutiliza un token robado dentro de la ventana no puede continuar la sesión: el token vigente pertenece al cliente legítimo.
- El `409` no revela identidad ni estado del usuario; el código es genérico.
- La ventana no se aplica a `Logout`, `ReuseDetected` ni `Expired`: esos motivos revocan la familia de inmediato aunque el instante sea reciente.
- Sin tokens en claro en BD: la rotación sigue persistiendo solo `TokenHash`.

## Contrato para el frontend

1. **Defensa principal:** serializar los refresh con single-flight (una sola promesa en vuelo) o Web Locks (`navigator.locks.request`) para que dos pestañas no roten el mismo token a la vez.
2. **Si aun así llega `409` (`Auth.RefreshConcurrent`):** reintentar como máximo **2 veces**, con **100–250 ms** de espera entre intentos, usando la cookie que ya tiene el navegador (el `409` no la borra).
3. Si el reintento vuelve a fallar, tratarlo como `401`: limpiar estado de sesión y redirigir al login.

## Referencias

- Auth0, *Refresh Token Rotation* (consulta 2026-11-20): rotación con detección automática de reuso y revocación de la familia. https://auth0.com/docs/secure/tokens/refresh-tokens/refresh-token-rotation
- RFC 9700, *Best Current Practice for OAuth 2.0 Security* (enero 2025), §2.2.2 y §4.14: rotación de refresh tokens para clientes públicos y protección contra replay. https://www.rfc-editor.org/rfc/rfc9700.html

## Consecuencias

- Dos refresh concurrentes legítimos ya no cierran la sesión: uno gana (200 + cookie nueva) y el otro recibe `409` sin tocar la cookie.
- El cliente debe implementar single-flight y el reintento acotado descrito arriba.
- La deuda #1 del ADR 0030 queda resuelta; #2 (vigencia absoluta de familia) y #3 (purga) siguen abiertas.
