namespace Pos.Application.Platform.Outbox.Queries;

using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Platform.Outbox.DTOs;
using Pos.Domain.Common;

/// <summary>
/// Consulta operativa para listar mensajes del Outbox en Dead-Letter de forma paginada y determinista.
/// Protegida con permiso de plataforma Permissions.Platform.DeadLetterView.
/// </summary>
[AllowsNoTenant]
[HasPermission(Permissions.Platform.DeadLetterView)]
public sealed record GetDeadLetterMessagesQuery(int PageNumber = 1, int PageSize = 50) : IQuery<Result<PagedResult<DeadLetterMessageDto>>>;

public sealed class GetDeadLetterMessagesQueryHandler : IQueryHandler<GetDeadLetterMessagesQuery, Result<PagedResult<DeadLetterMessageDto>>>
{
    private readonly IOutboxPlatformReader _reader;

    public GetDeadLetterMessagesQueryHandler(IOutboxPlatformReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<Result<PagedResult<DeadLetterMessageDto>>> HandleAsync(GetDeadLetterMessagesQuery request, CancellationToken cancellationToken = default)
    {
        var result = await _reader.GetDeadLetterMessagesAsync(request.PageNumber, request.PageSize, cancellationToken);
        return Result.Ok(result);
    }
}
