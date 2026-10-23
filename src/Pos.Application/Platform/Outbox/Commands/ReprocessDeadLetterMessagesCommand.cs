namespace Pos.Application.Platform.Outbox.Commands;

using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

/// <summary>
/// Caso de uso para reactivar mensajes en Dead-Letter del Outbox transaccional.
/// Protegido con permiso de plataforma Permissions.Platform.DeadLetterReprocess.
/// </summary>
[AllowsNoTenant]
[HasPermission(Permissions.Platform.DeadLetterReprocess)]
public sealed record ReprocessDeadLetterMessagesCommand(IReadOnlyList<Guid> MessageIds) : ICommand<Result<int>>;

public sealed class ReprocessDeadLetterMessagesCommandHandler : ICommandHandler<ReprocessDeadLetterMessagesCommand, Result<int>>
{
    private readonly IOutboxPlatformReader _reader;

    public ReprocessDeadLetterMessagesCommandHandler(IOutboxPlatformReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<Result<int>> HandleAsync(ReprocessDeadLetterMessagesCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MessageIds.Count == 0)
        {
            return Result.Ok(0);
        }

        var reprocessedCount = await _reader.ReprocessDeadLetterMessagesAsync(request.MessageIds, cancellationToken);
        return Result.Ok(reprocessedCount);
    }
}
