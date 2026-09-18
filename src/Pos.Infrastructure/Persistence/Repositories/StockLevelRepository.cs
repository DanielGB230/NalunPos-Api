using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class StockLevelRepository : IStockLevelRepository
{
    private readonly PosDbContext _context;

    public StockLevelRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(StockLevel stockLevel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stockLevel);
        await _context.StockLevels.AddAsync(stockLevel, cancellationToken);
    }

    public async Task<StockLevel?> GetAsync(
        Guid productId,
        Guid warehouseId,
        Guid? containerId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.StockLevels
            .FirstOrDefaultAsync(s =>
                s.ProductId == productId &&
                s.WarehouseId == warehouseId &&
                s.ContainerId == containerId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<StockLevel>> GetByWarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        return await _context.StockLevels
            .Where(s => s.WarehouseId == warehouseId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockLevel>> GetBelowThresholdAsync(Guid? warehouseId, CancellationToken cancellationToken = default)
    {
        var query = _context.StockLevels
            .Where(s => s.QuantityAvailable <= s.MinStockThreshold)
            .AsNoTracking();

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(s => s.WarehouseId == warehouseId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public void Update(StockLevel stockLevel)
    {
        ArgumentNullException.ThrowIfNull(stockLevel);
        _context.StockLevels.Update(stockLevel);
    }
}
