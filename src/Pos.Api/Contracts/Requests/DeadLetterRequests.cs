namespace Pos.Api.Contracts.Requests;

/// <summary>
/// Contrato de solicitud para consultar mensajes dead-letter.
/// </summary>
public sealed record GetDeadLetterMessagesRequest
{
    public int? Take { get; init; }
    public int? PageSize { get; init; }
}

/// <summary>
/// Contrato de solicitud para reprocesar mensajes dead-letter.
/// </summary>
public sealed record ReprocessDeadLetterMessagesRequest(IReadOnlyList<Guid> MessageIds);
