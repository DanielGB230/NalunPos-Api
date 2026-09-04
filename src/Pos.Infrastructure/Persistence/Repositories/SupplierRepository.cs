using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly ApplicationDbContext _context;

    public SupplierRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Supplier?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxId);
        return await _context.Suppliers.FirstOrDefaultAsync(s => s.TaxId.Value == taxId.Value, cancellationToken);
    }

    public async Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        bool? isActiveOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Suppliers.AsNoTracking().AsQueryable();

        if (isActiveOnly.HasValue)
        {
            query = query.Where(s => s.IsActive == isActiveOnly.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(s =>
                EF.Functions.Like(s.Name, pattern) ||
                EF.Functions.Like(s.TaxId.Value, pattern) ||
                EF.Functions.Like(s.ContactName, pattern) ||
                EF.Functions.Like(s.Email, pattern));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxId);
        return await _context.Suppliers.AnyAsync(s => s.TaxId.Value == taxId.Value && (excludeId == null || s.Id != excludeId.Value), cancellationToken);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        await _context.Suppliers.AddAsync(supplier, cancellationToken);
    }

    public void Update(Supplier supplier)
    {
        _context.Suppliers.Update(supplier);
    }

    public void Delete(Supplier supplier)
    {
        _context.Suppliers.Remove(supplier);
    }
}
