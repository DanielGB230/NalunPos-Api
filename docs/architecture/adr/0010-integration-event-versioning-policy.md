# ADR 0010: Política de Versionado de Integration Events

- **Estado:** Aceptado
- **Fecha:** 2026-09-01
- **Contexto:** Los contratos de eventos de integración en `Application/IntegrationEvents/Contracts` son consumidos de forma asíncrona por brokers de mensajería (RabbitMQ) y futuros microservicios (incluidos los de Inteligencia Artificial). Un cambio en la estructura de un evento sin versionar rompería los consumidores en producción.

## Decisión
Establecer una política de versionado explícito desde el primer día:

1. **Ubicación por Versión:** Todos los contratos de eventos de integración concretos deben residir dentro de subcarpetas de versión en `Pos.Application.IntegrationEvents.Contracts` (`V1/`, `V2/`, etc.).
2. **Nomenclatura Sufijada por Versión:** Los tipos de eventos llevarán la versión en su nombre (ej. `SaleCompletedIntegrationEventV1`).
3. **Cambios Compatibles:** La adición de campos opcionales está permitida dentro de una misma versión.
4. **Cambios Incompatibles (Breaking Changes):** Cambios de tipo o eliminación de campos obligan a crear una nueva versión (ej. `V2`) que convivirá con la versión anterior hasta que todos los consumidores migren.

## Consecuencias
- **Positivas:** Previene rupturas catastróficas en microservicios independientes o módulos consumiendo eventos vía EventBus.
- **Negativas:** Requiere mantener múltiples versiones de un evento durante periodos de transición.
