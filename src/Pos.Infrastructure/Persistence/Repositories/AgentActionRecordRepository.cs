using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class AgentActionRecordRepository : IAgentActionRecordRepository
{
    private readonly PosDbContext _context;

    public AgentActionRecordRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AgentActionRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AgentActionRecords.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AgentActionRecord>> GetPendingActionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AgentActionRecords
            .AsNoTracking()
            .Where(a => a.Status == AgentActionStatus.PendingApproval)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AgentActionRecord> Items, int TotalCount)> GetPendingActionsPagedAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var query = _context.AgentActionRecords
            .AsNoTracking()
            .Where(a => a.Status == AgentActionStatus.PendingApproval);

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsPendingActionAsync(string agentId, string proposedActionType, string payloadJson, CancellationToken cancellationToken = default)
    {
        string normAgent = agentId.Trim();
        string normType = proposedActionType.Trim();
        string normPayload = payloadJson.Trim();

        return await _context.AgentActionRecords.AnyAsync(a =>
            a.Status == AgentActionStatus.PendingApproval &&
            EF.Functions.Like(a.AgentId, normAgent) &&
            EF.Functions.Like(a.ProposedActionType, normType) &&
            a.PayloadJson == normPayload, cancellationToken);
    }

    public async Task AddAsync(AgentActionRecord record, CancellationToken cancellationToken = default)
    {
        await _context.AgentActionRecords.AddAsync(record, cancellationToken);
    }

    public void Update(AgentActionRecord record)
    {
        _context.AgentActionRecords.Update(record);
    }
}
