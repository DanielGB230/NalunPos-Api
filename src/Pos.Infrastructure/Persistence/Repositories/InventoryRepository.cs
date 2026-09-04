using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly ApplicationDbContext _context;

    public InventoryRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
    {
        await _context.InventoryMovements.AddAsync(movement, cancellationToken);
    }

    public async Task<decimal> GetCurrentStockAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        // Suma Kardex calculada directamente en la base de datos mediante SUM(...) de EF Core
        decimal totalStock = await _context.InventoryMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .SumAsync(m => (decimal?)m.Quantity, cancellationToken) ?? 0m;

        return totalStock;
    }

    public async Task<(IReadOnlyList<InventoryMovement> Items, int TotalCount)> GetMovementsHistoryPagedAsync(
        Guid productId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId);

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
