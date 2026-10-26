# ADR 0022: Outbox Concurrente con Lease Atómico, Semántica At-Least-Once e Idempotencia, y Gestión de Dead-Letter

- **Estado:** Aceptado
- **Fecha:** 2026-10-22
- **Contexto:** Escalar horizontalmente el procesamiento de eventos de integración mediante múltiples réplicas concurrentes de `OutboxProcessorBackgroundService` sin incurrir en entregas duplicadas simultáneas, documentar la garantía de entrega y definir los mecanismos de gobernanza operativa para mensajes en Dead-Letter.

---

## 1. Problema Previo

1. **Condición de Carrera en Múltiples Réplicas:**
   - La consulta original de `OutboxProcessorBackgroundService` seleccionaba mensajes pendientes basándose en `ProcessedOnUtc == null && !IsDeadLetter`.
   - Si dos o más instancias de la API se ejecutaban en paralelo, ambas leían el mismo lote de mensajes y lo publicaban concurrentemente en el bus de eventos (`IEventBus`), produciendo tormentas de eventos duplicados.
2. **Falta de Especificación de Idempotencia:**
   - No estaba formalmente normada la semántica de entrega del outbox ni la exigencia de idempotencia para los consumidores de eventos de integración.
3. **Ausencia de Vía Operativa para Dead-Letter:**
   - Cuando un mensaje agotaba `MaxRetries`, quedaba archivado con `IsDeadLetter = true`, pero no existía ningún caso de uso o interfaz de plataforma para consultarlo o reintentarlo de manera controlada.

---

## 2. Decisión Arquitectónica

### A. Reclamo Concurrente mediante Lease Atómico y Puerto IOutboxLeaseClaimer
- Se incorporan los campos `LockedUntilUtc` (timestamp) y `LockedBy` (identificador de instancia/réplica) en la entidad `OutboxMessage`, indexados junto a `ProcessedOnUtc`, `IsDeadLetter` y `NextRetryAtUtc`.
- Se aísla el reclamo de lease y la persistencia del outbox tras el puerto `IOutboxLeaseClaimer` (`Pos.Application.Common.Interfaces`) y su implementación `SqlOutboxLeaseClaimer` (`Pos.Infrastructure.Persistence.Outbox`), erradicando cualquier SQL crudo y eliminando condicionales de proveedor de base de datos (`IsRelational`) del background worker `OutboxProcessorBackgroundService` (patrón H11).
- La implementación SQL Server reclama el lote de mensajes de forma atómica utilizando la técnica de colas transaccionales de SQL Server:
  ```sql
  WITH cte AS (
      SELECT TOP (@BatchSize) *
      FROM dbo.OutboxMessages WITH (UPDLOCK, READPAST)
      WHERE ProcessedOnUtc IS NULL
        AND IsDeadLetter = 0
        AND (NextRetryAtUtc IS NULL OR NextRetryAtUtc <= @Now)
        AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= @Now)
      ORDER BY OccurredOnUtc
  )
  UPDATE cte
  SET LockedUntilUtc = @LeaseUntil, LockedBy = @InstanceId
  OUTPUT INSERTED.*;
  ```
- **Semántica de Concurrencia:** `UPDLOCK` bloquea las filas seleccionadas inmediatamente; `READPAST` instruye a cualquier réplica concurrente a omitir silenciosamente las filas bloqueadas por otra réplica y tomar las subsiguientes, eliminando colisiones y bloqueos distribuidos.

### B. Semántica At-Least-Once e Idempotencia de Consumidores
- El patrón Transactional Outbox ofrece semántica de entrega **At-Least-Once** (al menos una vez). Si una réplica cae después de despachar un evento al bus pero antes de registrar `ProcessedOnUtc`, el lease expirará (`LockedUntilUtc <= now`) y otra réplica volverá a procesarlo.
- **Regla no negociable:** Todos los suscriptores (`IIntegrationEventHandler<T>`) deben ser idempotentes por diseño. El procesamiento debe basarse en el `EventId` del mensaje o en operaciones seguras ante duplicados (upserts, idempotency keys o verificaciones de versión de agregado).

### C. Consulta y Reproceso Operativo de Dead-Letter
- Se introduce el puerto `IOutboxPlatformReader` (`Pos.Application.Common.Interfaces`) y su implementación `OutboxPlatformReader` (`Pos.Infrastructure.Persistence`), operando cross-tenant bajo privilegios de plataforma.
- Se implementan los casos de uso:
  - `GetDeadLetterMessagesQuery`: consulta paginada protegida por `[HasPermission(Permissions.Platform.DeadLetterView)]`.
  - `ReprocessDeadLetterMessagesCommand`: restablece `IsDeadLetter = false`, `RetryCount = 0`, `Error = null` y limpia locks para permitir la reevaluación del mensaje, protegido por `[HasPermission(Permissions.Platform.DeadLetterReprocess)]`.

### D. Invariante Matemática de Duración del Lease y Timeouts de Procesamiento
Para garantizar que un lease atómico no expire mientras una réplica legítima aún se encuentra procesando secuencialmente los mensajes restantes de su lote reclamado (`claimedMessages`), se establece y custodia la siguiente invariante matemática estricta:

$$\text{BatchSize} \times \text{MessageTimeoutSeconds} < \text{LeaseDurationSeconds}$$

1. **Acotamiento por Mensaje:** Cada mensaje individual se procesa bajo un `CancellationTokenSource` vinculado con timeout de `MessageTimeoutSeconds` (por defecto 2 segundos). Ningún mensaje puede exceder este umbral sin ser cancelado y registrado como fallo.
2. **Cálculo de Peor Escenario:** En el peor escenario de ejecución síncrona/secuencial del lote completo, el tiempo máximo total es $\text{BatchSize} \times \text{MessageTimeoutSeconds}$ (ej. $20 \times 2\text{s} = 40\text{s}$). Al ser estrictamente menor que `LeaseDurationSeconds` (ej. 60 segundos), se garantiza una holgura mínima de 20 segundos antes de que cualquier otra réplica concurrente pueda considerar el lock expirado.
3. **Validación en Tiempo de Inicio (Fail-Fast):** `OutboxSettings` implementa `IValidatableObject`. Si la configuración viola la condición $\text{BatchSize} \times \text{MessageTimeoutSeconds} \ge \text{LeaseDurationSeconds}$, la API aborta inmediatamente el inicio (`ValidateDataAnnotations()`, `ValidateOnStart()`), evitando despliegues mal configurados.

---

## 3. Consecuencias

- **Positivas:**
  - Cero entregas duplicadas concurrentes entre réplicas paralelas de background workers.
  - Tolerancia total a caídas de réplicas gracias a la expiración automática del lease (*lease timeout*).
  - Claridad de diseño: consumidores preparados para duplicados bajo semántica At-Least-Once.
  - Capacidad operativa completa para diagnosticar y reprocesar mensajes problemáticos en Dead-Letter.
- **Negativas:**
  - Las consultas directas de outbox en SQL Server requieren índices compuestos para optimizar `UPDLOCK, READPAST`.
