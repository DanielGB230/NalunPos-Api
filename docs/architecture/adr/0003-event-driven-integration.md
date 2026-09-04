# ADR 0003: Arquitectura Orientada a Eventos e Integración Asíncrona via RabbitMQ

- **Estado:** Aprobado
- **Fecha:** 2026-08-26
- **Contexto:** Las transacciones críticas del POS (como registrar una venta) deben ser ultra-rápidas y de alta disponibilidad. Invocar servicios externos o microservicios de IA de forma síncrona en la transacción de venta introduce latencia impredecible y puntos únicos de fallo.

## Decisión

1. Adopción de **Event-Driven Architecture (EDA)** asíncrona para toda comunicación que cruce bounded contexts o servicios externos.
2. Definir contratos de eventos de integración (`IIntegrationEvent`) en `Pos.Application/IntegrationEvents/Contracts`.
3. `Pos.Application` solo depende de la abstracción `IEventBus`.
4. `Pos.Infrastructure/EventBus/RabbitMq` encapsulará la integración concreta con RabbitMQ cuando se active la mensajería funcional.

## Consecuencias

- **Positivas:** Desacoplamiento total, las ventas procesan instantáneamente sin depender de la latencia o disponibilidad de servicios de IA o correo.
- **Negativas:** Consistencia eventual entre el POS y servicios consumidores.
