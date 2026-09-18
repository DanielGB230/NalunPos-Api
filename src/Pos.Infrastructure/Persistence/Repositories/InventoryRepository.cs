using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly PosDbContext _context;

    public InventoryRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movement);
        await _context.InventoryMovements.AddAsync(movement, cancellationToken);
    }

    public async Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsPagedAsync(
        Guid? productId,
        Guid? warehouseId,
        InventoryMovementType? movementType,
        DateTime? dateFrom,
        DateTime? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryMovements.AsNoTracking();

        if (productId.HasValue && productId.Value != Guid.Empty)
            query = query.Where(m => m.ProductId == productId.Value);

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
            query = query.Where(m => m.WarehouseId == warehouseId.Value);

        if (movementType.HasValue)
            query = query.Where(m => m.MovementType == movementType.Value);

        if (dateFrom.HasValue)
            query = query.Where(m => m.OccurredAtUtc >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(m => m.OccurredAtUtc <= dateTo.Value);

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.OccurredAtUtc)
            .ThenByDescending(m => m.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<decimal> ReconcileStockFromLedgerAsync(
        Guid productId,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        return await _context.InventoryMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId && m.WarehouseId == warehouseId)
            .SumAsync(m => (decimal?)m.Quantity, cancellationToken) ?? 0m;
    }
}
