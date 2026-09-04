using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class PurchaseRepository : IPurchaseRepository
{
    private readonly ApplicationDbContext _context;

    public PurchaseRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Purchases
            .Include(p => p.LineItems)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Purchase?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        string normalized = orderNumber.Trim();
        return await _context.Purchases
            .Include(p => p.LineItems)
            .FirstOrDefaultAsync(p => EF.Functions.Like(p.OrderNumber, normalized), cancellationToken);
    }

    public async Task<(IReadOnlyList<Purchase> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? supplierId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Purchases.AsNoTracking().Include(p => p.LineItems).AsQueryable();

        if (supplierId.HasValue && supplierId.Value != Guid.Empty)
        {
            query = query.Where(p => p.SupplierId == supplierId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(p => p.CreatedAtUtc <= endDate.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Purchase purchase, CancellationToken cancellationToken = default)
    {
        await _context.Purchases.AddAsync(purchase, cancellationToken);
    }

    public void Update(Purchase purchase)
    {
        _context.Purchases.Update(purchase);
    }
}
