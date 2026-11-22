using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Revoca en bloque todas las sesiones activas (refresh tokens) de un usuario.
/// La tabla <c>RefreshTokens</c> no está bajo RLS: la operación se ejecuta sobre el
/// <see cref="PosDbContext"/> de la petición sin abrir <see cref="IAuthenticationSessionScope"/>
/// ni ampliar el alcance global de usuarios (ADR 0034).
/// </summary>
public class UserSessionRevoker : IUserSessionRevoker
{
    private readonly PosDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public UserSessionRevoker(PosDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<int> RevokeAllAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del usuario no puede estar vacío.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        DateTime revokedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        return await _dbContext.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.RevokedAtUtc, revokedAtUtc)
                .SetProperty(r => r.RevokedReason, reason), cancellationToken);
    }
}
