# ADR 0029 — Vigencia del access token: fuente única y duración configurable

## Estado

Aceptado

## Contexto

Antes de este paso la expiración del access token JWT se calculaba dos veces y de forma independiente:

- `JwtTokenGenerator` (Infrastructure) fijaba `SecurityTokenDescriptor.Expires = utcNow.AddMinutes(60)`, que produce el claim `exp`.
- `LoginCommandHandler` (Application) fijaba `expiresAtUtc = timeProvider.GetUtcNow().UtcDateTime.AddHours(1)`, que se devuelve como `LoginResponse.ExpiresAtUtc`.

Ambos valores coincidían por casualidad (dos relojes leídos con milisegundos de diferencia), pero no existía una
fuente de verdad. Un cambio en uno de los dos —o una duración configurable aplicada solo al generador— habría
divergido en silencio: el cliente recibiría un `ExpiresAtUtc` distinto del `exp` real del token. Además, la
duración estaba codificada en dos literales (60 minutos y 1 hora) sin validación de arranque.

## Decisión

### Fuente única de la vigencia

`ITokenGenerator.GenerateToken` deja de devolver `string` y devuelve `AccessTokenResult`, un `sealed record` en
`Pos.Application/Common/Interfaces` con `Token` y `ExpiresAtUtc`. El generador calcula `expiresAtUtc` **una sola
vez** con `TimeProvider` y lo usa para:

1. `SecurityTokenDescriptor.Expires`, que se serializa como claim `exp`.
2. El `ExpiresAtUtc` del resultado, que `LoginCommandHandler` copia en `LoginResponse` sin calcular nada.

El handler ya no depende de `TimeProvider` y no contiene ninguna aritmética de expiración. `JwtSecurityTokenHandler`
trunca `exp` a segundos; por eso la comparación con `ExpiresAtUtc` admite una tolerancia de 1 segundo.

### Duración configurable y validada

`AccessTokenLifetimeOptions` (Infrastructure) aporta `AccessTokenLifetimeMinutes`:

- Valor por defecto: **60 minutos**.
- Rango validado: **5 a 120 minutos** (`[Range(5, 120)]`).
- Se enlaza desde la misma sección `JwtSettings` que `Secret`/`Issuer`/`Audience`. La clase solo aporta la
  duración; **no duplica** el manejo del secreto ni del emisor/audiencia, que siguen en `Pos.Api/Options/JwtSettings`.
- Se registra en `Program.cs` con `.Bind(...).ValidateDataAnnotations().ValidateOnStart()`: un valor fuera de rango
  impide el arranque (fail-fast).

### Guarda de arquitectura

`TokenExpirationGuardScanner` (Roslyn) escanea `SourceTreeIndex.ApplicationAndApi` y falla si encuentra:

- variables o asignaciones cuyo identificador contiene `expir` inicializadas con aritmética temporal
  (`AddHours`/`AddMinutes`/`AddSeconds`/`AddDays` o `+ TimeSpan`);
- asignaciones a `.Expires` (propiedad del descriptor JWT).

La guarda está sellada con ratchet en cero y se ejecuta en `AuthenticationGovernanceTests`, extendiendo la guarda
de escaneo de autenticación existente. Se demostró que falla contra el cálculo real previo en
`LoginCommandHandler.cs` y que pasa tras el cambio.

## Consecuencias

### Positivas

- El claim `exp` y `LoginResponse.ExpiresAtUtc` provienen del mismo cálculo y no pueden divergir.
- La duración se cambia en un único punto (`JwtSettings:AccessTokenLifetimeMinutes`) con validación de arranque.
- Un valor inválido (fuera de 5–120) impide arrancar la API en lugar de producir tokens con vigencia inesperada.
- La guarda impide reintroducir cálculos de expiración en Application o Pos.Api.
- El contrato OpenAPI no cambia: la forma de `LoginResponse` es idéntica.

### Negativas / Trade-offs

- El generador depende de `IOptions<AccessTokenLifetimeOptions>`, lo que obliga a registrar la opción en los
  hosts que resuelvan `ITokenGenerator` (la API la registra en `Program.cs`).

### Deuda explícita (Fase S)

1. **Revocación inmediata de access tokens:** un token emitido sigue siendo válido hasta su `exp`; no hay
   blacklist ni versión de sesión.
2. **Refresh tokens:** no existen; al expirar el access token el usuario debe autenticarse de nuevo.
3. **Sesiones y cookies:** la vigencia es *stateless*; no hay sesiones de servidor, cierre de sesión global ni
   cookies. Todo ello queda explícitamente para la Fase S.
4. **ClockSkew:** la validación mantiene `ClockSkew = 1 minuto` en `AuthenticationExtensions`; este ADR no lo
   modifica.

## Referencias

- ADR 0016 (guardas de arquitectura con Roslyn).
- ADR 0024 (snapshot y gobernanza del contrato OpenAPI).
- ADR 0026 (contrato de tiempo UTC y nombres fail-closed).
- ADR 0028 (control de acceso por estado de tenant).
