# ADR 0018: Estándares 2026 — TimeProvider BCL, Validación Temprana de Opciones y Gobernanza CI

- **Estado:** Aceptado
- **Fecha:** 2026-10-21
- **Contexto:** Modernización y consolidación de la arquitectura de NalunPos-Api bajo .NET 10 para garantizar determinismo temporal, configuración robusta a prueba de fallos tempranos y seguridad en la cadena de suministro de dependencias.

## Decisiones

### 1. Migración del 100% de `src/` a `System.TimeProvider` (.NET 10 BCL) y Guarda Ratchet en Cero (0)
- **Problema previo:** El uso directo de `DateTime.UtcNow`, `DateTime.Now`, `DateTimeOffset.UtcNow` y `DateTimeOffset.Now` introducía estado global no determinista, impidiendo pruebas unitarias con precisión temporal exacta sobre entidades de dominio y eventos.
- **Decisión:** 
  - Todo el código fuente de producción (`src/`) consume exclusivamente la abstracción nativa `System.TimeProvider` de la BCL de .NET 10, con cero dependencias externas o paquetes NuGet en `Pos.Domain`.
  - Las entidades de dominio permiten recibir `TimeProvider? timeProvider = null` en factorías y métodos de mutación, capturando el timestamp una sola vez por operación y sincronizándolo de forma atómica con los eventos de dominio (`IDomainEvent`).
  - La guarda de arquitectura basada en Roslyn (`SystemClockRatchetScanner`) mantiene sellada permanentemente su lista permitida en cero elementos (`AllowedDomainEntitiesRatchet.Count == 0`), garantizando tolerancia cero ante cualquier reintroducción de APIs de reloj del sistema.

### 2. Validación de Opciones en Inicio (`ValidateDataAnnotations` + `ValidateOnStart`)
- **Problema previo:** El uso directo de `.Configure<TOptions>()` difería la resolución de configuración errónea o incompleta hasta el primer request HTTP que intentara consumir `IOptions<TOptions>`, provocando caídas tardías en tiempo de ejecución.
- **Decisión:**
  - Queda prohibido el registro sin validación en `Pos.Api`. Todas las clases de opciones vinculadas a configuración (`JwtSettings`, `SuperAdminSettings`, etc.) deben configurarse mediante la API tipada:
    ```csharp
    builder.Services.AddOptions<TOptions>()
        .Bind(builder.Configuration.GetSection("..."))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    ```
  - Esto garantiza que cualquier inconsistencia o ausencia de variables de entorno críticas aborte el arranque del host de forma inmediata (Fail-Fast en despliegue).
  - Se respalda con la prueba arquitectónica `Options_In_Api_Must_Use_ValidateOnStart_And_Not_Direct_Configure`.

### 3. Política Free-First, `NuGetAudit` Bloqueante y CI en GitHub Actions
- **Problema previo:** Vulnerabilidades transitivas y posibles introducciones accidentales de paquetes con licencias comerciales incompatibles o proveedores de datos no autorizados.
- **Decisión:**
  - `Directory.Build.props` habilita de manera global `<NuGetAudit>true</NuGetAudit>`, `<NuGetAuditLevel>moderate</NuGetAuditLevel>`, `<NuGetAuditMode>all</NuGetAuditMode>` y `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>`.
  - En modo Release o integración continua, cualquier advertencia de compilación se trata como error (`/p:TreatWarningsAsErrors=true`).
  - En GitHub Actions CI (`.github/workflows/ci.yml`), el pipeline ejecuta restauración de dependencias, compilación con advertencias como error, ejecución del 100% de las suites de pruebas (`Domain`, `Application`, `Architecture`, `Infrastructure`, `Integration`) y validación de políticas Free-First mediante pruebas automatizadas con ArchUnitNET y Roslyn.

## Consecuencias

- **Positivas:**
  - 100% determinismo en pruebas unitarias y de integración sin mocks estáticos ni hacks de reflexión.
  - Arranque confiable y predecible de los servicios en producción gracias a Fail-Fast de configuración.
  - Blindaje completo contra vulnerabilidades y deudas de licenciamiento comercial.
- **Negativas:**
  - Mayor rigor en la definición de constructores y métodos mutadores de entidades, debiendo considerar el paso del `TimeProvider` cuando se requiere determinismo.
