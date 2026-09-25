using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly PosDbContext _context;

    public PurchaseOrderRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        await _context.PurchaseOrders.AddAsync(order, cancellationToken);
    }

    public async Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        PurchaseOrderStatus? status = null,
        Guid? supplierId = null,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PurchaseOrders
            .Include(p => p.Lines)
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (supplierId.HasValue && supplierId.Value != Guid.Empty)
        {
            query = query.Where(p => p.SupplierId == supplierId.Value);
        }

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(p => p.WarehouseId == warehouseId.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Update(PurchaseOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        _context.PurchaseOrders.Update(order);
    }
}
