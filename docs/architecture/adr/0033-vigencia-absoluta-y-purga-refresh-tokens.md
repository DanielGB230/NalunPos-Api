# ADR 0033: Vigencia Absoluta de la Familia y Purga de Refresh Tokens

- **Estado:** Aceptado
- **Fecha:** 2026-11-21
- **Contexto:** Cierra las deudas #2 y #3 del ADR 0030. Una familia de refresh tokens podía vivir indefinidamente mientras se rotara, y la tabla `RefreshTokens` crecía sin límite porque no existía purga automática.

## Decisión

### 1. Vigencia absoluta de la familia
- Nueva columna `RefreshTokens.FamilyExpiresAtUtc` (`DATETIME2`, `NOT NULL`, UTC).
- El **primer token de la familia** (login) la fija como `CreatedAtUtc + FamilyAbsoluteLifetime`.
- Todo **token rotado hereda** `FamilyExpiresAtUtc` sin recalcularla.
- `ExpiresAtUtc` de cada token es `min(ahora + vigencia deslizante, FamilyExpiresAtUtc)`. Así las comprobaciones de expiración existentes siguen siendo válidas y ningún token sobrevive a su familia.
- El dominio expone dos operaciones explícitas, `RefreshToken.CreateInitial(...)` y `RefreshToken.CreateRotated(...)`, con `TimeProvider` como única fuente de tiempo y validación de instantes UTC. No se usa `DateTime.UtcNow`.

### 2. Configuración
- `RefreshToken:FamilyAbsoluteDays` con defecto **30** y rango `[1, 365]`.
- `RefreshTokenOptions.FamilyAbsoluteLifetime` deriva de esos días.
- Validación cruzada al arranque: `FamilyAbsoluteDays >= RefreshTokenDays`. Se aplica con `IValidatableObject` y con `.Validate(...)` en `Program.cs`, junto a `ValidateDataAnnotations()` + `ValidateOnStart()`. Los mensajes están en español.
- El `Max-Age` de la cookie se calcula desde el vencimiento **efectivo** devuelto por `IssueAsync`/`RotateAsync` (`ExpiresAtUtc − ahora`), nunca desde las opciones, con mínimo de 1 segundo y sin valores negativos.

### 3. Migración
- Migración `AddRefreshTokenFamilyExpiryAndPurgeIndex`, posterior a `AddRefreshTokens`.
- Estrategia sin pérdida de filas: columna nullable → `UPDATE FamilyExpiresAtUtc = ExpiresAtUtc` → `ALTER COLUMN ... NOT NULL`.
- Índice filtrado `IX_RefreshTokens_RevokedAtUtc (WHERE RevokedAtUtc IS NOT NULL)`. Ya existía `IX_RefreshTokens_ExpiresAtUtc`.

### 4. Purga por lotes
- Puerto Application `IRefreshTokenPurger.PurgeAsync(DateTime nowUtc, TimeSpan retention, int batchSize, CancellationToken) → total eliminado`.
- Implementación Infrastructure con `ExecuteDeleteAsync` sobre una subquery de IDs y `TOP(batchSize)`: no se cargan entidades en memoria.
- Es elegible un token cuando su revocación es anterior a `nowUtc − retention`, o cuando nunca fue revocado y su expiración es anterior a ese corte. Nunca se eliminan tokens activos, ni revocados/expirados dentro de la retención.
- El bucle continúa hasta que un lote devuelve menos de `batchSize`. Cada lote es atómico e idempotente; dos instancias pueden ejecutar a la vez sin conflicto funcional.

### 5. BackgroundService
- `RefreshTokenPurgeBackgroundService` sigue el patrón de `OutboxProcessorBackgroundService`: `PeriodicTimer`, `IServiceScopeFactory`, un scope por ejecución y `try/catch` que registra y continúa. Un método `internal PurgeAsync` permite invocarlo desde pruebas de integración.
- `RefreshTokenPurgeSettings`: `Enabled=true`, `IntervalMinutes=60`, `RetentionDays=7`, `BatchSize=1000`, con rangos y `ValidateOnStart`.
- Si `Enabled=false`, el servicio no crea el timer ni programa ninguna ejecución.
- Solo se registra a nivel `Information` la cantidad eliminada cuando es mayor que cero.

## Alternativas descartadas

1. **Recalcular la vigencia absoluta en cada rotación.** No limita la familia: un cliente (o un atacante con el token) podría seguir rotando y extendiendo la sesión indefinidamente. La vigencia absoluta debe fijarse una vez por familia.
2. **Mantener solo `ExpiresAtUtc` sin columna de familia.** Cada rotación reinicia el contador y no hay forma de distinguir el límite absoluto del deslizante. La columna es necesaria para heredar el límite entre tokens.
3. **Purga con un job externo o un paquete (Quartz/Hangfire).** Introduce una dependencia y un proceso adicional para una tarea periódica simple. `BackgroundService` nativo cubre el caso sin paquetes nuevos (política de dependencias gratuitas).
4. **Purgar sin retención.** Eliminar tokens en cuanto expiran o se revocan rompe la detección de reuso: un token robado y presentado después de la purga parecería desconocido y no se podría distinguir de un reuso real. La retención conserva la evidencia durante la ventana configurada.

## Seguridad

- **Límite duro de sesión:** aunque el token se rote, la familia muere como máximo a los `FamilyAbsoluteDays` desde el login. Reduce la ventana de un refresh token robado.
- **Detección de reuso intacta:** la retención (7 días por defecto) conserva los tokens revocados/expirados el tiempo necesario para clasificar reuso y rotación concurrente.
- **Purga conservadora:** no elimina tokens activos ni revocados/expirados dentro de la retención. La regla de elegibilidad exige que la revocación sea antigua, o que el token nunca revocado esté expirado y sea antiguo.
- **Multiinstancia:** el borrado por lotes no usa estado en memoria ni transacciones largas; es idempotente y seguro con varias instancias.
- **Sin RLS en `RefreshTokens`:** la tabla no tiene `TenantId`; el acceso sigue gobernándose por `TokenHash` y `FamilyId` mediante `IAuthenticationSessionScope` (ADR 0030). La purga es una operación de mantenimiento sin contexto de usuario.

## Escalabilidad

- Borrado por lotes acotados (`BatchSize`, defecto 1000), sin cargar entidades y sin mantener una transacción durante todo el barrido.
- `IX_RefreshTokens_ExpiresAtUtc` sostiene la búsqueda de expirados; `IX_RefreshTokens_RevokedAtUtc` filtrado sostiene la de revocados.
- El intervalo (60 minutos por defecto) y el tamaño de lote son configurables para adaptar la carga a la base.

## Claves de configuración

| Sección | Clave | Defecto | Rango / invariante |
|---|---|---|---|
| `RefreshToken` | `FamilyAbsoluteDays` | `30` | `[1, 365]` y `>= RefreshTokenDays` |
| `RefreshTokenPurge` | `Enabled` | `true` | Booleano |
| `RefreshTokenPurge` | `IntervalMinutes` | `60` | `[1, 1440]` |
| `RefreshTokenPurge` | `RetentionDays` | `7` | `[1, 90]` |
| `RefreshTokenPurge` | `BatchSize` | `1000` | `[1, 5000]` |

## Consecuencias para el front

- La sesión termina como máximo a los `FamilyAbsoluteDays` (30 por defecto) desde el login, aunque el cliente rote el refresh token con normalidad.
- Al vencer la familia, `POST /api/v1/auth/refresh` responde `401` con código `Auth.RefreshTokenInvalid` y limpia la cookie `nalunpos_rt` (flujo ya contemplado en el ADR 0032). El front debe redirigir al login.
- Un nuevo login crea una familia nueva y reinicia el contador absoluto.
- El `Max-Age` de la cookie se recorta al tiempo restante de la familia, de modo que la cookie no sobreviva al token.
- **No cambia el contrato OpenAPI**: no se añaden ni modifican endpoints, códigos ni esquemas HTTP.

## Referencias

- ADR 0030 (Refresh tokens rotativos y cookie HttpOnly), ADR 0031 (ventana de tolerancia), ADR 0032 (limpieza de cookie).
- ADR 0018 (TimeProvider), ADR 0026 (UTC).
