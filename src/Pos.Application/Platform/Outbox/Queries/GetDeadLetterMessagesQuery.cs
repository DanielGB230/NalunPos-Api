namespace Pos.Application.Platform.Outbox.Queries;

using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Outbox.DTOs;
using Pos.Domain.Common;

/// <summary>
/// Consulta operativa para listar mensajes del Outbox en Dead-Letter.
/// Protegida con permiso de plataforma Permissions.Platform.DeadLetterView.
/// </summary>
[AllowsNoTenant]
[HasPermission(Permissions.Platform.DeadLetterView)]
public sealed record GetDeadLetterMessagesQuery(int Take = 50) : IQuery<Result<IReadOnlyList<DeadLetterMessageDto>>>;

public sealed class GetDeadLetterMessagesQueryHandler : IQueryHandler<GetDeadLetterMessagesQuery, Result<IReadOnlyList<DeadLetterMessageDto>>>
{
    private readonly IOutboxPlatformReader _reader;

    public GetDeadLetterMessagesQueryHandler(IOutboxPlatformReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<Result<IReadOnlyList<DeadLetterMessageDto>>> HandleAsync(GetDeadLetterMessagesQuery request, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(request.Take, 1, 100);
        var messages = await _reader.GetDeadLetterMessagesAsync(take, cancellationToken);
        return Result.Ok(messages);
    }
}
