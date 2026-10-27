namespace Pos.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Platform.Outbox.DTOs;
using Pos.Infrastructure.Persistence.Context;

/// <summary>
/// Implementación del puerto de plataforma para consultar y reactivar mensajes Dead-Letter del Outbox.
/// </summary>
public sealed class OutboxPlatformReader : IOutboxPlatformReader
{
    private readonly PosDbContext _dbContext;

    public OutboxPlatformReader(PosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PagedResult<DeadLetterMessageDto>> GetDeadLetterMessagesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OutboxMessages.Where(m => m.IsDeadLetter);

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (pageNumber - 1) * pageSize;

        var items = await query
            .OrderByDescending(m => m.OccurredOnUtc)
            .ThenByDescending(m => m.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(m => new DeadLetterMessageDto(
                m.Id,
                m.TenantId,
                m.Type,
                m.Content,
                m.OccurredOnUtc,
                m.RetryCount,
                m.Error))
            .ToListAsync(cancellationToken);

        return new PagedResult<DeadLetterMessageDto>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<int> ReprocessDeadLetterMessagesAsync(IEnumerable<Guid> messageIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messageIds);

        var targetIds = messageIds.ToList();
        var messages = await _dbContext.OutboxMessages
            .Where(m => targetIds.Contains(m.Id) && m.IsDeadLetter)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.ReprocessFromDeadLetter();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }
}
