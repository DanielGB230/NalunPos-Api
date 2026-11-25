using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Pos.Infrastructure.Caching;

namespace Pos.IntegrationTests.Fixtures;

/// <summary>
/// Invalidación explícita de la caché de estado de tenant en las pruebas de integración.
/// Única responsabilidad: resolver <see cref="HybridCache"/> y eliminar la clave del estado
/// del tenant. Las pruebas no dependen del TTL de la caché (ADR 0028).
/// </summary>
public static class TenantStatusCacheControl
{
    /// <summary>
    /// Elimina de la caché la entrada de estado del tenant indicado, para que la siguiente
    /// lectura vea el estado persistido en lugar de un valor obsoleto.
    /// </summary>
    public static async Task InvalidateAsync(
        IServiceProvider services,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var cache = services.GetRequiredService<HybridCache>();
        await cache.RemoveAsync(TenantCacheKeys.TenantStatusForTenant(tenantId), cancellationToken);
    }
}
