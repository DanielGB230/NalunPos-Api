# ADR 0004: Capa Anti-Corrupción (ACL) para Capacidades de IA

- **Estado:** Aprobado
- **Fecha:** 2026-08-26
- **Contexto:** El sistema POS requerirá integraciones con Inteligencia Artificial (pronóstico de demanda, detección de fraude, recomendaciones, visión por computadora). Acoplar el núcleo de aplicación o de dominio a SDKs de proveedores específicos (OpenAI, Anthropic, Azure OpenAI, ONNX runtime) genera dependencia directa de terceros y riesgo de obsolescencia cuando las APIs de los modelos cambien.

## Decisión

1. Diseñar una **Capa Anti-Corrupción (ACL)** estricta para toda capacidad de IA.
2. `Pos.Application/AI/Abstractions` definirá únicamente las interfaces y contratos agnósticos requeridos por el negocio (ej. `IDemandForecastCapability`, `IFraudSignalCapability`).
3. `Pos.Infrastructure/AiOrchestration/Adapters` contendrá las implementaciones concretas que traducen las solicitudes del sistema a los formatos específicos de los SDKs o microservicios de IA.
4. `Pos.Domain` y `Pos.Application` nunca importarán ni dependerán de SDKs, paquetes NuGet o clientes HTTP específicos de proveedores de IA.

## Consecuencias

- **Positivas:** Libertad total para cambiar de proveedor de IA, alternar entre servicios Cloud y modelos locales sin tocar la lógica del negocio.
- **Negativas:** Obliga a crear tipos de datos agnósticos y adaptadores explicitos de traducción para cada capacidad de IA.
