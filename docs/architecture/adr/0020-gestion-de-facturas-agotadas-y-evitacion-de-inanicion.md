# ADR 0020: Gestión de Facturas Agotadas, Prevención de Inanición de Lotes y Alertas Fiscales Desacopladas

- **Estado:** Aceptado
- **Fecha:** 2026-10-22
- **Contexto:** Garantizar la continuidad operativa del proceso de reconciliación fiscal en segundo plano (`InvoiceReconciliationBackgroundService`), erradicando la inanición de lotes (*batch starvation*) producida por facturas con intentos agotados y eliminando la fuga de reglas de negocio hacia la capa de presentación/hosting (`Pos.Api`).

---

## 1. Problema Previo

1. **Inanición de Lote (*Batch Starvation*):**
   - El lector de plataforma de facturas pendientes (`IPendingInvoicePlatformReader`) seleccionaba facturas en base únicamente a `Status == Pending` y `IssueDateUtc <= cutoff`, ordenadas cronológicamente y limitadas por `.Take(batchSize)`.
   - Cuando un grupo de facturas fallaba reiteradamente hasta alcanzar `MaxReconciliationAttempts`, permanecían con `Status == Pending`.
   - Al ser las más antiguas, continuaban copando el `Take(batchSize)` en cada ciclo de ejecución, impidiendo que facturas más recientes fueran leídas o procesadas.
2. **Fuga de Reglas de Negocio en `Pos.Api`:**
   - La validación del umbral de intentos (`invoice.ReconciliationAttempts >= settings.MaxReconciliationAttempts`) y la emisión de alertas fiscales críticas residían directamente en `InvoiceReconciliationBackgroundService` (proyecto `Pos.Api`).
   - Esto violaba el principio de Clean Architecture según el cual `Pos.Api` es únicamente un adaptador de hosting/temporización, mientras que las reglas de negocio y políticas fiscales pertenecen exclusivamente a `Pos.Domain` y `Pos.Application`.
3. **Dependencias Temporales Implícitas:**
   - Los servicios en segundo plano contenían constructores con `TimeProvider? timeProvider = null` y fallback tácito a `TimeProvider.System`, contraviniendo el estándar determinista del ADR 0018.

---

## 2. Decisión Arquitectónica

### A. Filtrado en Puerto de Infraestructura (`IPendingInvoicePlatformReader`)
- Se incorpora el parámetro `int maxAttempts` en el contrato del puerto `IPendingInvoicePlatformReader.GetPendingInvoicesAsync(int thresholdMinutes, int maxAttempts, int batchSize, CancellationToken ct)`.
- La consulta SQL Server filtra directamente por `i.ReconciliationAttempts < maxAttempts`. Las facturas agotadas son omitidas inmediatamente de la cola de conciliación periódica, permitiendo que las nuevas facturas vencidas se procesen sin inanición.

### B. Estado de Dominio `RequiresManualIntervention` y Regla de Negocio
- Se introduce en `InvoiceStatus` el estado terminal/operativo `RequiresManualIntervention = 5`.
- Se implementa el método `Invoice.MarkAsRequiresManualIntervention(string reason, TimeProvider timeProvider)` en el aggregate root `Invoice`.
- En el caso de uso `ReconcileInvoiceCommandHandler` (`Pos.Application`), se verifica si la factura ya alcanzó o superó `MaxAttempts`. De ser así:
  - Se invoca `invoice.MarkAsRequiresManualIntervention(...)`.
  - Se retorna un fallo determinista: `Result.Fail<InvoiceDto>(DomainError.Conflict("Invoice.ReconciliationExhausted", ...))`.
  - No se realiza ninguna llamada al proveedor de facturación electrónica (`IElectronicInvoicingService`).

### C. Alerta Fiscal Desacoplada vía Eventos de Dominio
- El agregado `Invoice` emite el evento de dominio `InvoiceReconciliationExhaustedDomainEvent`.
- En `Pos.Application`, el manejador `InvoiceReconciliationExhaustedDomainHandler` captura dicho evento y emite el registro crítico estructurado (`ALERTA FISCAL`).
- El worker en segundo plano `InvoiceReconciliationBackgroundService` queda exclusivamente con responsabilidades de infraestructura y orquestación temporal: consultar el lector, abrir scopes de tenant, y despachar `ReconcileInvoiceCommand`.

### D. Inyección Estricta de `TimeProvider`
- Se elimina cualquier parámetro opcional o fallback `= null` en los constructores de `InvoiceReconciliationBackgroundService` y `OutboxProcessorBackgroundService`.
- `TimeProvider` es una dependencia obligatoria inyectada por el contenedor de inversión de control (`IServiceProvider`).

---

## 3. Consecuencias

- **Positivas:**
  - Inmunidad total a la inanición de lotes: las facturas válidas continúan procesándose en el mismo ciclo independientemente de cuántas facturas agotadas existan en la base de datos.
  - Reglas de negocio e invariantes fiscales 100% protegidas en `Pos.Domain` y `Pos.Application`.
  - `Pos.Api` mantiene su rol estricto de adaptador de infraestructura y hosting.
  - Verificación determinista y pruebas automatizadas confiables mediante inyección explícita de `TimeProvider` y tests de concurrencia e inanición.
- **Negativas / Operativas:**
  - Las facturas en estado `RequiresManualIntervention` quedan a la espera de un flujo administrativo o comando de remediación manual por parte del operador contable.
