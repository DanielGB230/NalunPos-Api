using Pos.Application.Tests.Support.Fakes;
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

}
