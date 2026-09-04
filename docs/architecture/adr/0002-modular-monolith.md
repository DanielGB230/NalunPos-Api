# ADR 0002: Adopción de Monolito Modular (Modular Monolith)

- **Estado:** Aprobado
- **Fecha:** 2026-08-26
- **Contexto:** Diseñar microservicios prematuros para un sistema POS introduce una complejidad operativa devastadora (latencia de red, consistencia distribuida, despliegues coordinados) antes de entender profundamente los límites reales del dominio.

## Decisión

1. Organizar `Pos.Application` como un **Monolito Modular** utilizando empaquetado por funcionalidad (*vertical slicing*).
2. Cada módulo (ej. `Products`, `Sales`, `Inventory`) reside en su propia carpeta con límites conceptuales de Bounded Context bien definidos.
3. La comunicación inter-módulos dentro del monolito debe preferir eventos de integración (`IntegrationEvents`) sobre llamadas directas cuando cruce fronteras de dominio.

## Consecuencias

- **Positivas:** Despliegue simple, baja latencia, desarrollo ágil en etapas iniciales.
- **Negativas:** Requiere evitar acoplamientos accidentales mediante referencias directas de clases entre carpetas de distintos módulos.
