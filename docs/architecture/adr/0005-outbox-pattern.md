# ADR 0005: Garantía de Atomicidad con Transactional Outbox Pattern

- **Estado:** Aprobado
- **Fecha:** 2026-08-26
- **Contexto:** En arquitectura orientada a eventos, publicar eventos directamente en un broker de mensajería (RabbitMQ) dentro del mismo flujo que la transacción de base de datos genera el problema del *Dual Write*: si la base de datos se confirma pero el bus de eventos falla (o viceversa), el sistema entra en un estado inconsistente irreparable.

## Decisión

1. Adoptar el patrón **Transactional Outbox** para la publicación de eventos de integración.
2. Reservar la estructura en `Pos.Infrastructure/Persistence/Outbox`.
3. Al ejecutar una operación de negocio, los eventos de integración se persisten en la misma transacción relacional de la base de datos (tabla Outbox).
4. Un proceso asíncrono en segundo plano (*Outbox Processor*) leerá las publicaciones pendientes de la tabla Outbox y las despachará hacia el `IEventBus` (RabbitMQ) garantizando entrega al menos una vez (*At-least-once delivery*).

## Consecuencias

- **Positivas:** Consistencia transaccional garantizada entre almacenamiento persistente y eventos de integración sin depender de transacciones distribuidas (2PC).
- **Negativas:** Introduce una latencia de milisegundos en el despacho de eventos de integración y requiere manejo de idempotencia en los consumidores.
