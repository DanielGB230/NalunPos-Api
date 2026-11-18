using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Enums;
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Caching;

/// <summary>
/// Implementación de infraestructura de <see cref="ITenantStatusReader"/>.
/// Lee el estado de ciclo de vida de un Tenant desde <see cref="PosDbContext"/> con caché
/// <see cref="HybridCache"/> para evitar lecturas repetidas por petición autenticada.
/// </summary>
/// <remarks>
/// La tabla <c>Tenants</c> NO está bajo Row-Level Security (RLS), por lo que esta lectura
/// por identificador explícito es válida desde cualquier contexto (tenant, plataforma,
/// o dentro de callbacks de <see cref="HybridCache"/> bajo <c>ExecutionContext.SuppressFlow()</c>).
/// No requiere <see cref="ITenantSetter"/> porque no filtra por el tenant actual.
/// El TTL de la caché se configura mediante <see cref="TenantStatusOptions"/> (por defecto 60 s),
/// definiendo el retardo máximo de propagación de un cambio de estado.
/// </remarks>
public sealed class TenantStatusReader : ITenantStatusReader
{
    private readonly HybridCache _cache;
    private readonly PosDbContext _dbContext;
    private readonly int _ttlSeconds;

    public TenantStatusReader(
        HybridCache cache,
        PosDbContext dbContext,
        IOptions<TenantStatusOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _ttlSeconds = options?.Value?.TenantStatusCacheTtlSeconds ?? 60;
    }

    public async Task<TenantStatus?> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == PlatformScope.TenantId)
        {
            return null;
        }

        string cacheKey = TenantCacheKeys.TenantStatusForTenant(tenantId);

        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromSeconds(_ttlSeconds)
        };

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var tenant = await _dbContext.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == tenantId, cancel);

                return tenant is null ? (TenantStatus?)null : tenant.Status;
            },
            entryOptions,
            cancellationToken: cancellationToken);
    }
}
