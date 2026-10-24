namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Modelo inmutable de mensaje del Outbox transaccional reclamado bajo lease atómico para su procesamiento.
/// </summary>
public sealed record ClaimedOutboxMessage(
    Guid Id,
    Guid? TenantId,
    string Type,
    string Content,
    int RetryCount);

/// <summary>
/// Puerto para el reclamo atómico y actualización de estado de mensajes del Outbox transaccional.
/// </summary>
public interface IOutboxLeaseClaimer
{
    /// <summary>
    /// Reclama atómicamente un lote de mensajes pendientes aplicando un lease temporal exclusivo.
    /// </summary>
    Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimMessagesAsync(
        string instanceId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un mensaje reclamado como procesado exitosamente y libera su lease.
    /// </summary>
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra el fallo de procesamiento de un mensaje, programando reintento exponencial o dead-letter.
    /// </summary>
    Task RecordFailureAsync(
        Guid messageId,
        string errorMessage,
        int maxRetries,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default);
}
