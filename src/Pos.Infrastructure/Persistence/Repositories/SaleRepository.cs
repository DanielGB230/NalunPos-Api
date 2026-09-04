using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class SaleRepository : ISaleRepository
{
    private readonly PosDbContext _context;

    public SaleRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Sales
            .Include(s => s.LineItems)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Sale?> GetByReceiptNumberAsync(string receiptNumber, CancellationToken cancellationToken = default)
    {
        string normalized = receiptNumber.Trim();
        return await _context.Sales
            .Include(s => s.LineItems)
            .FirstOrDefaultAsync(s => EF.Functions.Like(s.ReceiptNumber, normalized), cancellationToken);
    }

    public async Task<(IReadOnlyList<Sale> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? sessionId,
        Guid? customerId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Sales.AsNoTracking().Include(s => s.LineItems).AsQueryable();

        if (sessionId.HasValue && sessionId.Value != Guid.Empty)
        {
            query = query.Where(s => s.SessionId == sessionId.Value);
        }

        if (customerId.HasValue && customerId.Value != Guid.Empty)
        {
            query = query.Where(s => s.CustomerId == customerId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(s => s.CreatedAtUtc >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.CreatedAtUtc <= endDate.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        await _context.Sales.AddAsync(sale, cancellationToken);
    }

    public void Update(Sale sale)
    {
        _context.Sales.Update(sale);
    }
}
