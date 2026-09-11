using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Tenants.DTOs;
using Pos.Application.Platform.Tenants.Queries.GetTenants;
using Pos.Domain.Entities;
using Xunit;

namespace Pos.Application.Tests.Platform.Tenants;

public class GetTenantsQueryHandlerTests
{
    private readonly FakeTenantRepository _tenantRepository = new();
    private readonly GetTenantsQueryHandler _handler;

    public GetTenantsQueryHandlerTests()
    {
        _handler = new GetTenantsQueryHandler(_tenantRepository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPagedTenants()
    {
        // Arrange
        _tenantRepository.Tenants.Add(Tenant.Create("Tenant Alpha S.A.", "20100000001"));
        _tenantRepository.Tenants.Add(Tenant.Create("Tenant Beta EIRL", "20100000002"));

        var query = new GetTenantsQuery(1, 10, null);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Tenant Alpha S.A.", result.Items[0].Name);
        Assert.Equal("20100000001", result.Items[0].DocumentNumber);
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public List<Tenant> Tenants { get; } = [];

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Tenants.FirstOrDefault(t => t.Id == id));
        }

        public Task<bool> ExistsByTaxIdAsync(string taxId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Tenants.Any(t => t.TaxId.Value.Equals(taxId, StringComparison.OrdinalIgnoreCase)));
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
}
