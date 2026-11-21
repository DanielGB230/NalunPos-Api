using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Purga de refresh tokens fuera de la ventana de retención (F1.G.6 / ADR 0033).
/// Elimina por lotes mediante <c>ExecuteDeleteAsync</c>, sin cargar entidades.
/// Un token es elegible cuando su revocación es anterior al corte, o cuando nunca
/// fue revocado y su expiración es anterior al corte. Así nunca se elimina un token
/// revocado o expirado dentro de la retención (la retención sostiene la detección
/// de reuso de la familia).
/// </summary>
public class RefreshTokenPurger : IRefreshTokenPurger
{
    private readonly PosDbContext _dbContext;

    public RefreshTokenPurger(PosDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<int> PurgeAsync(
        DateTime nowUtc,
        TimeSpan retention,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("El instante de purga debe ser UTC.", nameof(nowUtc));
        }

        if (retention < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retention), "La retención no puede ser negativa.");
        }

        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "El tamaño de lote debe ser al menos 1.");
        }

        DateTime cutoffUtc = nowUtc - retention;
        int totalDeleted = 0;

        while (true)
        {
            var batchIds = _dbContext.RefreshTokens
                .Where(r =>
                    (r.RevokedAtUtc != null && r.RevokedAtUtc < cutoffUtc) ||
                    (r.RevokedAtUtc == null && r.ExpiresAtUtc < cutoffUtc))
                .OrderBy(r => r.Id)
                .Select(r => r.Id)
                .Take(batchSize);

            int deleted = await _dbContext.RefreshTokens
                .Where(r => batchIds.Contains(r.Id))
                .ExecuteDeleteAsync(cancellationToken);

            totalDeleted += deleted;

            if (deleted < batchSize)
            {
                break;
            }
        }

        return totalDeleted;
    }
}
