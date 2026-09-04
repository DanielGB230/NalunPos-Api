namespace Pos.Infrastructure.Persistence.Outbox;

/// <summary>
/// Entidad de infraestructura para el patrón Transactional Outbox (Sección 10).
/// Guarda los eventos de integración producidos dentro de la misma transacción de base de datos.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset OccurredOnUtc { get; private set; }
    public DateTimeOffset? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
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
        Type = type.Trim();
        Content = content;
        OccurredOnUtc = occurredOnUtc;
    }

    public static OutboxMessage Create(Guid id, string type, string content, DateTimeOffset occurredOnUtc)
    {
        return new OutboxMessage(id, type, content, occurredOnUtc);
    }

    public void MarkAsProcessed()
    {
        ProcessedOnUtc = DateTimeOffset.UtcNow;
        Error = null;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
    }
}
