using Microsoft.Extensions.Caching.Hybrid;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Caching;

/// <summary>
/// Implementación de infraestructura de <see cref="ITenantStatusCacheInvalidator"/>.
/// Única responsabilidad: eliminar de <see cref="HybridCache"/> la clave de estado del tenant.
/// No lee ni modifica <see cref="ITenantStatusReader"/>.
/// </summary>
/// <remarks>
/// <see cref="HybridCache"/> registrado por defecto solo tiene L1 en memoria: la invalidación
/// afecta únicamente a la instancia local. En despliegues multi-instancia, el TTL configurado
/// en <see cref="TenantStatusOptions"/> sigue siendo la cota de propagación entre instancias.
/// </remarks>
public sealed class TenantStatusCacheInvalidator : ITenantStatusCacheInvalidator
{
    private readonly HybridCache _cache;

    public TenantStatusCacheInvalidator(HybridCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task InvalidateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(TenantCacheKeys.TenantStatusForTenant(tenantId), cancellationToken);
    }
}
