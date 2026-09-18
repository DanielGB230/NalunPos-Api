using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly PosDbContext _context;

    public StockAdjustmentRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adjustment);
        await _context.StockAdjustments.AddAsync(adjustment, cancellationToken);
    }

    public async Task<StockAdjustment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StockAdjustments
            .Include(a => a.Lines)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StockAdjustments
            .Include(a => a.Lines)
            .AsNoTracking();

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
