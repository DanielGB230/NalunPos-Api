namespace Pos.Application.Common.Interfaces;

using Pos.Application.Common.Models;
using Pos.Application.Platform.Outbox.DTOs;

/// <summary>
/// Puerto de plataforma para consultar y gestionar mensajes en Dead-Letter del Outbox transaccional.
/// </summary>
public interface IOutboxPlatformReader
{
    /// <summary>Obtiene los mensajes en estado Dead-Letter paginados con total.</summary>
    Task<PagedResult<DeadLetterMessageDto>> GetDeadLetterMessagesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Restablece los mensajes indicados para que vuelvan a ser procesados por el OutboxProcessor.</summary>
    Task<int> ReprocessDeadLetterMessagesAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken = default);
}
