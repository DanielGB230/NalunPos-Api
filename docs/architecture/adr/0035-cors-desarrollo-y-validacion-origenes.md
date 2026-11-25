# ADR 0035: CORS desarrollo y validación de orígenes

## Contexto

En el entorno de desarrollo de Nalun POS, las peticiones cross-origin desde el frontend Angular (puerto 4200/4201) deben estar permitidas, mientras que en producción se requiere un enfoque fail-closed (sólo los orígenes explicitamente configurados). El requisito surgió del hueco de seguridad descrito en ADR 0032, donde desactivar un usuario no invalidaba sus refresh tokens, y es necesario asegurar que solo los orígenes permitidos puedan comunicarse con la API.

## Decisión

### 1. Orígenes por entorno (base vacía + desarrollo)

- **`src/Pos.Api/appsettings.json`**: `CorsSettings:AllOrigins` → lista vacía `[]` (fail-closed por defecto).
- **`src/Pos.Api/appsettings.Development.json`**: 4 orígenes `http://localhost:4200`, `https://localhost:4200`, `http://localhost:4201`, `https://localhost:4201`.

### 2. Patrón de validación (reutilizando estructura existente)

La validación de orígenes sigue el mismo patrón que `OutboxSettings` y `RefreshTokenOptions`:

- **Clase `CorsSettings`** implementa `IValidatableObject` con método `Validate(ValidationContext)`.
- **Registro en `Program.cs`**: `.AddOptions<CorsSettings>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`.
- **Mensajes de error**: incluyen la clave `CorsSettings:AllowedOrigins` y el valor ofensor, consistentes con el patrón del repo.
- **Regla de validación** (`CorsOriginsRules`): URI absoluta; esquema http/https; host presente; sin path (ni "/"), query, fragment ni userinfo; no "*" ni "null".

### 3. Política CORS construida desde opciones validadas

- **`AddCustomCors`** ya no lee configuración cruda ni duplica validación: la política se construye desde `IOptions<CorsSettings>` ya validadas por `ValidateOnStart`.
- **Fuente única de verdad**: `Program.cs` valida al arranque; la extensión usa esas opciones validadas.
- **Método "AllowFrontend"**: nombre de política idéntico al registro original (`app.UseCors("AllowFrontend")`), manteniendo contrato con el middleware.

### 4. Alternativas descartadas

- **`SetIsOriginAllowed`**: vulnerabilidad documentada (permite cualquier origen en runtime); eliminado.
- **Wildcard `*`**: no cumple el requisite de fail-closed; prohibido.
- **Proxy de Angular como sustituto de CORS**: arquitectura rechazada; CORS es el mecanismo correcto.
- **`IValidateOptions<CorsSettings>`**: no existe en el repo; el patrón vigente es `IValidatableObject` + `ValidateDataAnnotations()` + `ValidateOnStart`.

### 5. SameSite=Strict y cookies con `Path=/api/v1/auth`

- Al usar `CorsSettings` con orígenes de `localhost` puertos distintos (4200/4201), se considera el mismo sitio para `SameSite=Strict` siempre que el `Path` sea `/api/v1/auth` (como definido en `CookieAssertions.cs`). Esto permite que las cookies de refresh token funcionen correctamente en desarrollo.

### 6. Contrato de despliegue

- Producción define sus orígenes mediante `CorsSettings__AllowedOrigins__N` en variables de entorno o config de producción.
- El pipeline de CI/CD debe establecer estos valores; no usar nunca lista vacía en producción.

## Alternativas consideradas y desechadas

| Alternativa | Razón de descarte |
|-------------|-------------------|
| `SetIsOriginAllowed(_ => true)` | Brecha de seguridad: cualquier origen en runtime |
| Lista `*` en `AllowedOrigins` | Fallo de diseño fail-closed |
| Usar `Configure<CorsOptions>` directamente | Duplica validación; rompe patrón Outbox/RefreshToken |
| `IValidateOptions<CorsSettings>` | No existe en el repo; requiere nuevo patrón |
| CORS via proxy Angular | Arquitectura anti-patrón; CORS es nativo |

## Notas de despliegue

- Desarrollo: `appsettings.Development.json` con 4 orígenes localhost:4200/4201.
- Producción: configurar `CorsSettings:AllowedOrigins` por entorno (no lista vacía).
- Nunca commitear lista vacía a producción sin configurar orígenes reales.