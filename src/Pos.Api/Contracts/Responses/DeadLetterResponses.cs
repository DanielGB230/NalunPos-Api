namespace Pos.Api.Contracts.Responses;

/// <summary>
/// Contrato de respuesta para un mensaje en estado dead-letter.
/// </summary>
public sealed record DeadLetterMessageResponse(
    Guid Id,
    Guid? TenantId,
    string Type,
    string Content,
    DateTimeOffset OccurredOnUtc,
    int RetryCount,
    string? Error);

/// <summary>
/// Contrato de respuesta para el resultado del reproceso de mensajes dead-letter.
/// </summary>
public sealed record ReprocessDeadLetterMessagesResponse(int ReprocessedCount);
