# ADR 0035: CORS desarrollo y validación de orígenes

## Estado

Aceptado.

## Contexto

En el entorno de desarrollo de Nalun POS, las peticiones cross-origin desde el frontend Angular (puertos 4200/4201) deben estar permitidas, mientras que en producción se requiere un enfoque fail-closed (sólo los orígenes explícitamente configurados).

La política CORS anterior se construía leyendo la configuración cruda dentro de `AddCustomCors` y abortaba el arranque con una excepción si la lista quedaba vacía. Eso dejaba la validación fuera del mecanismo de opciones del hosting (sin `ValidateOnStart`), sin mensaje normalizado por clave de configuración y sin regla de validación de la forma de cada origen: un origen mal formado (por ejemplo `http://localhost:4200/`, `*` o `ftp://localhost:4200`) se aceptaba silenciosamente y sólo fallaba en tiempo de ejecución de cada petición.

## Decisión

### 1. Orígenes por entorno (base vacía + desarrollo)

- **`src/Pos.Api/appsettings.json`**: `CorsSettings:AllowedOrigins` → lista vacía `[]` (fail-closed por defecto: sin origins no se admite ninguna petición cross-origin).
- **`src/Pos.Api/appsettings.Development.json`**: 4 orígenes `http://localhost:4200`, `https://localhost:4200`, `http://localhost:4201`, `https://localhost:4201`.

### 2. Patrón de validación (mismo esqueleto que `OutboxSettings`)

- **`src/Pos.Api/Options/CorsSettings.cs`**: POCO simple. Sin `IValidatableObject` y sin data annotations: la validación vive en una clase propia, no en el contenedor de datos.
- **`src/Pos.Api/Options/CorsOriginsRules.cs`**: clase propia y **pura** (no lee configuración, no registra servicios, no depende del pipeline de CORS). Responsabilidad única: decidir si una lista de orígenes es válida y construir el mensaje de error.
  - Reglas: lista no vacía; cada origen es una URI absoluta; esquema `http` o `https`; host presente; sin path (ni el `/` final), sin query, sin fragment y sin userinfo; no se admiten `*` ni `null`.
- **`src/Pos.Api/Options/CorsOriginsOptionsValidator.cs`**: adaptador de las reglas puras al mecanismo de opciones del hosting (`IValidateOptions<CorsSettings>`). Es la única forma de obtener un **mensaje dinámico** en .NET 10: `OptionsBuilder<T>.Validate` sólo admite un `string` estático capturado en el registro, por lo que el mensaje con el valor ofensor se construye en tiempo de validación.
- **Registro en `Program.cs`** (mismo patrón que `OutboxSettings`):

```csharp
builder.Services.AddSingleton<IValidateOptions<CorsSettings>, CorsOriginsOptionsValidator>();
builder.Services.AddOptions<CorsSettings>()
    .Bind(builder.Configuration.GetSection(CorsSettings.SectionName))
    .Validate(settings => CorsOriginsRules.HasValidOrigins(settings.AllowedOrigins), CorsOriginsRules.ValidationFailureMessage)
    .ValidateOnStart();
```

- **Mensajes de error**: la clave `CorsSettings:AllowedOrigins` y el valor ofensor (origen concreto) aparecen en el mensaje; el predicado registrado en `.Validate(...)` aporta el mensaje genérico con las reglas y el validador `IValidateOptions` añade el detalle del origen ofensor.
- **Orden de fail-fast**: tras `builder.Build()` las opciones CORS se resuelven y validan **antes** de `InitializeDatabaseAsync()`. Así un error de configuración no se enmascare con un fallo de base de datos. `ValidateOnStart()` mantiene además el fail-fast del host para el resto de caminos de arranque.

### 3. Política CORS construida desde opciones validadas

- **`AddCustomCors`** ya no lee configuración cruda ni duplica la comprobación de la lista: la política se construye desde `IOptions<CorsSettings>` ya validadas.
- **Fuente única de verdad**: `Program.cs` valida al arranque; la extensión usa esas opciones validadas.
- **Nombre de política**: `"AllowFrontend"`, idéntico al contrato con el middleware `app.UseCors("AllowFrontend")`. La política se registra sobre el nombre de opciones por defecto (`AddOptions<CorsOptions>()` sin nombre) para que la resuelva `DefaultCorsPolicyProvider`.
- **Métodos permitidos**: `GET`, `POST`, `PATCH`, `OPTIONS`; cualquier cabecera; credenciales permitidas.

### 4. Alternativas descartadas

- **`SetIsOriginAllowed`**: vulnerabilidad documentada (permite cualquier origen en runtime); eliminado.
- **Wildcard `*`**: no cumple el requisito de fail-closed; prohibido por regla de validación.
- **Proxy de Angular como sustituto de CORS**: arquitectura rechazada; CORS es el mecanismo correcto.
- **`ValidateDataAnnotations()` sobre el POCO**: no valida nada (el POCO no lleva anotaciones); la validación real es la de `CorsOriginsRules`.
- **Data annotations en `CorsSettings`**: acopla el POCO a `System.ComponentModel.DataAnnotations` y devuelve el problema al contenedor de datos; se mantiene el POCO simple.
- **Mensaje estático con el origen ofensor**: imposible con `OptionsBuilder<T>.Validate` en .NET 10 (sólo admite `string` estático); resuelto con `IValidateOptions<CorsSettings>`.

### 5. SameSite=Strict y cookies con `Path=/api/v1/auth`

Al usar `CorsSettings` con orígenes de `localhost` en puertos distintos (4200/4201), se considera el mismo sitio para `SameSite=Strict` siempre que el `Path` sea `/api/v1/auth` (como definido en `CookieAssertions.cs`). Esto permite que las cookies de refresh token funcionen correctamente en desarrollo.

### 6. Contrato de despliegue

- Producción define sus orígenes mediante `CorsSettings__AllowedOrigins__N` en variables de entorno o configuración de producción.
- El pipeline de CI/CD debe establecer estos valores; no usar nunca lista vacía en producción.

## Alternativas consideradas y desechadas

| Alternativa | Razón de descarte |
|-------------|-------------------|
| `SetIsOriginAllowed(_ => true)` | Brecha de seguridad: cualquier origen en runtime |
| Lista `*` en `AllowedOrigins` | Fallo de diseño fail-closed |
| Usar `Configure<CorsOptions>` directamente | Duplica validación; rompe patrón Outbox/RefreshToken |
| `ValidateDataAnnotations()` sobre POCO sin anotaciones | No valida nada: el arranque deja de ser fail-fast |
| Data annotations en `CorsSettings` | Acopla el POCO al contenedor de datos; la regla vive en `CorsOriginsRules` |
| CORS via proxy Angular | Arquitectura anti-patrón; CORS es nativo |

## Notas de despliegue

- Desarrollo: `appsettings.Development.json` con 4 orígenes localhost:4200/4201.
- Producción: configurar `CorsSettings:AllowedOrigins` por entorno (no lista vacía).
- Nunca commitear lista vacía a producción sin configurar orígenes reales.
- Las pruebas de integración deben declarar `CorsSettings:AllowedOrigins:0` en su fábrica: la lista vacía de `appsettings.json` aborta el arranque por diseño.
