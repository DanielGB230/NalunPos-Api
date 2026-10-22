namespace Pos.Infrastructure.Persistence.Outbox;

/// <summary>
/// Entidad de infraestructura para el patrón Transactional Outbox (Sección 10).
/// Guarda los eventos de integración producidos dentro de la misma transacción de base de datos.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset OccurredOnUtc { get; private set; }
    public DateTimeOffset? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset? NextRetryAtUtc { get; private set; }
    public bool IsDeadLetter { get; private set; }

    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        Guid? tenantId,
        string type,
        string content,
        DateTimeOffset occurredOnUtc)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("El tipo de evento no puede estar vacío.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("El contenido del evento no puede estar vacío.", nameof(content));
        }

        Id = id;
        TenantId = tenantId;
        Type = type.Trim();
        Content = content;
        OccurredOnUtc = occurredOnUtc;
        RetryCount = 0;
        IsDeadLetter = false;
    }

    public static OutboxMessage Create(Guid id, Guid? tenantId, string type, string content, DateTimeOffset occurredOnUtc)
    {
        return new OutboxMessage(id, tenantId, type, content, occurredOnUtc);
    }

    public void MarkAsProcessed(DateTimeOffset processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
        Error = null;
        NextRetryAtUtc = null;
    }

    public void MarkAsProcessed(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ProcessedOnUtc = timeProvider.GetUtcNow();
        Error = null;
        NextRetryAtUtc = null;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
        RetryCount++;
    }

    /// <summary>
    /// Registra un fallo en el procesamiento. Incrementa RetryCount. Si se alcanza maxRetries,
    /// pasa el mensaje a estado Dead-Letter (IsDeadLetter = true). Si no, programa el siguiente
    /// reintento aplicando retryDelay (backoff exponencial) mediante el timeProvider.
    /// </summary>
    public void RecordFailure(string error, int maxRetries, TimeSpan retryDelay, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        Error = error;
        RetryCount++;

        if (RetryCount >= maxRetries)
        {
            IsDeadLetter = true;
            NextRetryAtUtc = null;
        }
        else
        {
            IsDeadLetter = false;
            NextRetryAtUtc = timeProvider.GetUtcNow().Add(retryDelay);
        }
    }

    public void MarkAsDeadLetter(string error)
    {
        Error = error;
        IsDeadLetter = true;
        NextRetryAtUtc = null;
    }
}
