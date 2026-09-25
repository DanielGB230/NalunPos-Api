using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class StockTransferRepository : IStockTransferRepository
{
    private readonly PosDbContext _context;

    public StockTransferRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(StockTransfer transfer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        await _context.StockTransfers.AddAsync(transfer, cancellationToken);
    }

    public async Task<StockTransfer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StockTransfers
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<StockTransfer> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? sourceWarehouseId = null,
        Guid? destinationWarehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StockTransfers
            .Include(t => t.Lines)
            .AsNoTracking();

        if (sourceWarehouseId.HasValue && sourceWarehouseId.Value != Guid.Empty)
        {
            query = query.Where(t => t.SourceWarehouseId == sourceWarehouseId.Value);
        }

        if (destinationWarehouseId.HasValue && destinationWarehouseId.Value != Guid.Empty)
        {
            query = query.Where(t => t.DestinationWarehouseId == destinationWarehouseId.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
