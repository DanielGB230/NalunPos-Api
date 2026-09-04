using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly PosDbContext _context;

    public CustomerRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Customer?> GetByTaxIdAsync(TaxId taxId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxId);
        return await _context.Customers.FirstOrDefaultAsync(c => c.TaxId.Value == taxId.Value, cancellationToken);
    }

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        bool? isActiveOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Customers.AsNoTracking().AsQueryable();

        if (isActiveOnly.HasValue)
        {
            query = query.Where(c => c.IsActive == isActiveOnly.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(c =>
                EF.Functions.Like(c.FullName, pattern) ||
                EF.Functions.Like(c.TaxId.Value, pattern) ||
                EF.Functions.Like(c.Email, pattern) ||
                EF.Functions.Like(c.Phone, pattern));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> ExistsByTaxIdAsync(TaxId taxId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxId);
        return await _context.Customers.AnyAsync(c => c.TaxId.Value == taxId.Value && (excludeId == null || c.Id != excludeId.Value), cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _context.Customers.AddAsync(customer, cancellationToken);
    }

    public void Update(Customer customer)
    {
        _context.Customers.Update(customer);
    }

    public void Delete(Customer customer)
    {
        _context.Customers.Remove(customer);
    }
}
