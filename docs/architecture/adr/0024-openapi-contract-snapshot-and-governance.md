# ADR 0024: Snapshot Determinista del Contrato OpenAPI v1, Gobernanza de Schemas y Detección de Deriva

- **Estado:** Aceptado
- **Fecha:** 2026-11-12
- **Contexto:**
  1. La integración y sincronización frontend-backend entre `NalunPos-Api` y `NalunPos-Front` requiere una especificación formal, unívoca y automatizada de los endpoints HTTP, schemas JSON de solicitud/respuesta, códigos de estado y definiciones de seguridad.
  2. En .NET 10, herramientas de generación de OpenAPI basadas en MSBuild o extensiones en tiempo de compilación presentan graves incompatibilidades cuando la aplicación ejecuta tareas de inicialización y siembra determinista (`InitializeDatabaseAsync`) previas a `app.Run()`, además de requerir paquetes o herramientas externas adicionales.
  3. La exposición del documento OpenAPI (`/openapi/v1.json`) y de la consola interactiva Scalar (`/scalar`) en entornos productivos debe regirse por el principio de mínimo privilegio y seguridad *fail-closed*, quedando estrictamente deshabilitada fuera de `Development` sin flags de configuración en producción.
  4. Los controladores de la API utilizaban métodos de conveniencia (`ToActionResult` / `ToCreatedResult`) devolviendo `Task<IActionResult>`, lo cual omitía los metadatos de respuesta exitosa y de error tipado (`[ProducesResponseType]`), generando un documento OpenAPI incompleto con respuestas vacías o no tipadas en endpoints clave.
  5. Sin un mecanismo determinista de captura, normalización y comparación contra un archivo versionado en Git, cualquier cambio accidental o no autorizado en contratos de datos pasa inadvertido en CI, provocando fallas silenciosas en los clientes consumidores.

---

## Decisión

### 1. Extracción desde el Host de Pruebas (`WebApplicationFactory`) sin Paquetes Nuevos
- Se adopta la biblioteca nativa `Microsoft.AspNetCore.OpenApi` (.NET 10 nativo, ya presente en el proyecto). No se añaden paquetes NuGet ni herramientas MSBuild adicionales (0 paquetes nuevos en `Directory.Packages.props` y archivos `.csproj`).
- La extracción del documento OpenAPI v1 se realiza mediante la ejecución del host de pruebas en memoria (`OpenApiTestWebApplicationFactory`) bajo el entorno `"Testing"`.

### 2. Exposición Segura en `Program.cs` y Aislamiento en Pruebas
- En producción (`IsProduction()`), Staging o Testing estándar, los endpoints OpenAPI y Scalar permanecen inactivos y no se mapean.
- En `Program.cs`:
  ```csharp
  if (app.Environment.IsDevelopment())
  {
      app.MapOpenApi().AllowAnonymous();
      app.MapScalarApiReference("/scalar", options =>
      {
          options.WithTitle("Nalun POS API Documentation")
                 .WithTheme(ScalarTheme.Purple);
      }).AllowAnonymous();
  }
  else
  {
      app.UseHsts();
  }
  ```
- **Justificación de `.AllowAnonymous()` en `Development`:**
  `AuthenticationExtensions.cs` define un `FallbackPolicy` global (`RequireAuthenticatedUser`) para toda ruta sin atributo explícito (principio de seguridad *fail-closed*). En el entorno `Development`, los desarrolladores necesitan abrir `/scalar` y `/openapi/v1.json` directamente desde el navegador web o herramientas locales sin requerir la pre-autenticación de la solicitud ni el envío previo de cabeceras `Authorization: Bearer <token>`.
- En `OpenApiTestWebApplicationFactory`:
  - Se establece `builder.UseEnvironment("Testing")`, garantizando que `DatabaseInitializer.InitializeDatabaseAsync` no intente ejecutar `MigrateAsync()` sobre la base de datos in-memory.
  - Para extraer el documento en pruebas de integración sin comprometer el pipeline de producción ni requerir banderas de configuración en `src/`, se registra un `IStartupFilter` de pruebas exclusivo (`OpenApiTestStartupFilter`) que mapea `/openapi/v1.json` con `.AllowAnonymous()` en el pipeline en memoria del host de pruebas.
  - Se aísla el nombre de la base de datos in-memory (`NalunPosDb_OpenApi_{Guid}`) para garantizar independencia total respecto a otras suites de pruebas ejecutadas concurrentemente por xUnit.

### 3. Normalización Determinista del Snapshot JSON
- El snapshot OpenAPI se almacena versionado en `docs/api/openapi.v1.json`.
- La función de normalización (`OpenApiContractSnapshotTests.NormalizeOpenApiJson`) garantiza determinismo estricto:
  - Ordenamiento recursivo de claves de propiedades mediante `StringComparer.Ordinal`.
  - Eliminación completa de la propiedad `"servers"`, previniendo fugas de URLs locales, nombres de host o puertos efímeros.
  - Formateo uniforme con indentación de 2 espacios y terminación de líneas estándar `\n`.

### 4. Suite de Pruebas de Contrato (`OpenApiContractSnapshotTests.cs`)
- **Prueba Positiva (`OpenApiDocument_MatchesVersionedSnapshot_WithoutContractDrift`):** Consume `/openapi/v1.json` del host de pruebas, lo normaliza y lo compara recursivamente contra `docs/api/openapi.v1.json`. Si difieren, reporta la ruta JSON exacta de la discrepancia.
- **Prueba Negativa (`ContractDriftDetector_WhenContractIsAltered_DetectsDiscrepancyAndInstructsUpdate`):** Verifica que una alteración intencional en memoria del contrato sea detectada inmediatamente por el algoritmo de comparación, reportando el path afectado.
- **Verificación de Schemas y Seguridad (`OpenApiDocument_ContainsExpectedCanonicalSchemas_AndSecurityDefinitions`):**
  - Esquema de seguridad `Bearer` (`http` / `bearer` / `JWT`).
  - Schemas canónicos exportados con propiedades completas: `LoginResponse`, `LoginRequest`, `WarehouseDto`, `CreateWarehouseRequest`.
  - Schema de `ProblemDetails` enriquecido con la propiedad de extensión `"code"` (`string` opcional, RFC 9457 / ADR 0023).
  - Enums serializados como `string` con sus valores literales (`ConfigureHttpJsonOptions` con `JsonStringEnumConverter`).
- **Regeneración Explícita (Opt-In):** La actualización del archivo en disco jamás se realiza de forma implícita durante `dotnet test`. Requiere el establecimiento explícito de la variable de entorno `UPDATE_OPENAPI_SNAPSHOT=true`.

### 5. Metadatos de Controladores y Deuda Registrada
- Se actualizan con atributos explícitos `[Produces(MediaTypeNames.Application.Json)]` y `[ProducesResponseType(typeof(...), StatusCodes.Status...)]` los controladores del flujo inmediato de autenticación y almacenes:
  - `AuthController`: `Login` (200 con `LoginResponse`, 400 y 401 con `ProblemDetails`), `Ping` (200 con `string`).
  - `WarehousesController`: `GetWarehouses` (200 con `IReadOnlyList<WarehouseDto>`, 401 y 403 con `ProblemDetails`), `GetStockByWarehouse` (200 con `PagedResult<StockLevelDto>`, 400, 401, 403, 404 con `ProblemDetails`), `GetWarehouseMovements` (200 con `PagedResult<InventoryMovementDto>`, 400, 401, 403, 404 con `ProblemDetails`), `CreateWarehouse` (201 con `WarehouseDto`, 400, 401, 403, 409 con `ProblemDetails`).
- El resto de controladores existentes (21 controladores) se auditan y catalogan en el inventario de deuda técnica para su incorporación progresiva en fases subsiguientes.

---

## Consecuencias

### Positivas
- Prevención automatizada de regresiones de contrato en pipelines de CI/CD: cualquier cambio de contrato sin snapshot actualizado rompe la compilación/test de inmediato.
- Base sólida y determinista para la generación de tipos TypeScript y servicios HTTP en `NalunPos-Front` (Paso F1.B).
- Cumplimiento de políticas de seguridad: producción permanece hermética sin exponer esquemas OpenAPI ni consolas interactivas.
- Zero dependencias externas o paquetes nuevos introducidos.

### Negativas / Trade-offs
- Cualquier cambio intencional en rutas o DTOs requiere actualizar de forma deliberada el snapshot mediante `UPDATE_OPENAPI_SNAPSHOT=true` y revisar el diff de Git resultante.
