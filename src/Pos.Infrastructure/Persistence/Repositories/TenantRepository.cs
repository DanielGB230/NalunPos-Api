using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence.Context;

using Pos.Application.Platform.Tenants.DTOs;

namespace Pos.Infrastructure.Persistence.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly PosDbContext _context;

    public TenantRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsByTaxIdAsync(string taxId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taxId)) return false;
        var taxIdVo = new Pos.Domain.ValueObjects.CompanyTaxId(taxId.Trim());
        return await _context.Tenants.AnyAsync(t => t.TaxId == taxIdVo, cancellationToken);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _context.Tenants.AddAsync(tenant, cancellationToken);
    }

    public void Update(Tenant tenant)
    {
        _context.Tenants.Update(tenant);
    }

    public async Task<(IReadOnlyList<TenantDto> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tenants
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(t =>
                EF.Functions.Like(t.Name, pattern) ||
                EF.Functions.Like(t.TaxId, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TenantDto(
                t.Id,
                t.Name,
                t.TaxId.Value,
                t.Status.ToString(),
                t.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
