using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Caching;
using Xunit;

namespace Pos.Infrastructure.Tests;

public class TenantStatusCacheInvalidatorTests
{
    [Fact]
    public async Task InvalidateAsync_RemovesOnlyTheTenantStatusKey()
    {
        // Arrange
        var services = new ServiceCollection();
#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018
        services.AddScoped<ITenantStatusCacheInvalidator, TenantStatusCacheInvalidator>();

        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<HybridCache>();

        Guid invalidatedTenantId = Guid.NewGuid();
        Guid untouchedTenantId = Guid.NewGuid();
        string invalidatedKey = TenantCacheKeys.TenantStatusForTenant(invalidatedTenantId);
        string untouchedKey = TenantCacheKeys.TenantStatusForTenant(untouchedTenantId);

        await cache.GetOrCreateAsync(invalidatedKey, _ => ValueTask.FromResult("Suspended"));
        await cache.GetOrCreateAsync(untouchedKey, _ => ValueTask.FromResult("Active"));

        var invalidator = provider.GetRequiredService<ITenantStatusCacheInvalidator>();

        // Act
        await invalidator.InvalidateAsync(invalidatedTenantId);

        // Assert: la clave invalidada se reconstruye; la ajena sigue servida desde caché.
        int invalidatedFactoryCalls = 0;
        int untouchedFactoryCalls = 0;

        string invalidatedValue = await cache.GetOrCreateAsync(
            invalidatedKey,
            _ =>
            {
                invalidatedFactoryCalls++;
                return ValueTask.FromResult("Suspended");
            });

        string untouchedValue = await cache.GetOrCreateAsync(
            untouchedKey,
            _ =>
            {
                untouchedFactoryCalls++;
                return ValueTask.FromResult("Active");
            });

        Assert.Equal("Suspended", invalidatedValue);
        Assert.Equal("Active", untouchedValue);
        Assert.Equal(1, invalidatedFactoryCalls);
        Assert.Equal(0, untouchedFactoryCalls);
    }
}
