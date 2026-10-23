namespace Pos.Application.Platform.Outbox.DTOs;

/// <summary>
/// DTO con la información de un mensaje del Outbox que ha sido derivado a Dead-Letter.
/// </summary>
public sealed record DeadLetterMessageDto(
    Guid Id,
    Guid? TenantId,
    string Type,
    string Content,
    DateTimeOffset OccurredOnUtc,
    int RetryCount,
    string? Error);
