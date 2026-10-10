namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Tenants.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeTenantRepository : ITenantRepository
{
    public List<Tenant> Tenants { get; } = [];

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Tenants.FirstOrDefault(t => t.Id == id));
    }

    public Task<bool> ExistsByTaxIdAsync(string taxId, CancellationToken cancellationToken = default)
    {
        string normalized = taxId.Trim();
        return Task.FromResult(Tenants.Any(t => t.TaxId.Value.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        Tenants.Add(tenant);
        return Task.CompletedTask;
    }

    public void Update(Tenant tenant)
    {
    }

    public Task<(IReadOnlyList<TenantDto> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        var query = Tenants.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(t => t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                     t.TaxId.Value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        var list = query
            .Select(t => new TenantDto(t.Id, t.Name, t.TaxId.Value, t.Status.ToString(), t.CreatedAtUtc))
            .ToList();

        return Task.FromResult<(IReadOnlyList<TenantDto>, int)>((list, list.Count));
    }
}
