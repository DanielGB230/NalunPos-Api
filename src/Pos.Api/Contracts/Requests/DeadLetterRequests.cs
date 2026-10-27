namespace Pos.Api.Contracts.Requests;

/// <summary>
/// Contrato de solicitud para consultar mensajes dead-letter con paginación estándar.
/// </summary>
public sealed record GetDeadLetterMessagesRequest
{
    public int? PageNumber { get; init; }
    public int? PageSize { get; init; }
}

/// <summary>
/// Contrato de solicitud para reprocesar mensajes dead-letter.
/// </summary>
public sealed record ReprocessDeadLetterMessagesRequest(IReadOnlyList<Guid> MessageIds);
