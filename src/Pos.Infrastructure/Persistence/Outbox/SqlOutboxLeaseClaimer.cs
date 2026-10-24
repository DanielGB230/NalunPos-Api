namespace Pos.Infrastructure.Persistence.Outbox;

using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Persistence.Context;

/// <summary>
/// Implementación SQL Server de alta concurrencia para el reclamo de lease del Outbox transaccional.
/// Utiliza 'WITH (UPDLOCK, READPAST)' para garantizar que dos réplicas concurrentes nunca
/// reclamen ni procesen el mismo mensaje en paralelo (ADR 0022).
/// </summary>
public sealed class SqlOutboxLeaseClaimer : IOutboxLeaseClaimer
{
    private readonly PosDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public SqlOutboxLeaseClaimer(PosDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimMessagesAsync(
        string instanceId,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

        var now = _timeProvider.GetUtcNow();
        var leaseUntil = now.Add(leaseDuration);

        var rawSql = @"
            WITH cte AS (
                SELECT TOP ({0}) *
                FROM [dbo].[OutboxMessages] WITH (UPDLOCK, READPAST)
                WHERE [ProcessedOnUtc] IS NULL
                  AND [IsDeadLetter] = 0
                  AND ([NextRetryAtUtc] IS NULL OR [NextRetryAtUtc] <= {1})
                  AND ([LockedUntilUtc] IS NULL OR [LockedUntilUtc] <= {1})
                ORDER BY [OccurredOnUtc]
            )
            UPDATE cte
            SET [LockedUntilUtc] = {2}, [LockedBy] = {3}
            OUTPUT INSERTED.*;";

        var messages = await _dbContext.OutboxMessages
            .FromSqlRaw(rawSql, batchSize, now, leaseUntil, instanceId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return messages
            .Select(m => new ClaimedOutboxMessage(m.Id, m.TenantId, m.Type, m.Content, m.RetryCount))
            .ToList();
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message.MarkAsProcessed(_timeProvider);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RecordFailureAsync(
        Guid messageId,
        string errorMessage,
        int maxRetries,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message.RecordFailure(errorMessage, maxRetries, retryDelay, _timeProvider);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
