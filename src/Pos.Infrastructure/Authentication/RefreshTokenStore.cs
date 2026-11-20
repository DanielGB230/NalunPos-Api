using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Authentication;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Almacenamiento de refresh tokens rotativos.
/// Todas las operaciones ejecutan sobre <see cref="RefreshToken"/> (hash SHA-256,
/// nunca token claro) dentro de un <see cref="IAuthenticationSessionScope"/> para
/// manejar el <c>SESSION_CONTEXT</c> de SQL Server bajo RLS.
/// </summary>
public class RefreshTokenStore : IRefreshTokenStore
{
    private readonly PosDbContext _dbContext;
    private readonly IAuthenticationSessionScope _sessionScope;
    private readonly TimeProvider _timeProvider;

    public RefreshTokenStore(
        PosDbContext dbContext,
        IAuthenticationSessionScope sessionScope,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _sessionScope = sessionScope ?? throw new ArgumentNullException(nameof(sessionScope));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RefreshTokenCandidate?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        await using var session = await _sessionScope.BeginAsync(cancellationToken);
        await session.EnableGlobalUserLookupAsync(cancellationToken);
        try
        {
            var entity = await _dbContext.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

            if (entity is null)
            {
                return null;
            }

            return new RefreshTokenCandidate(
                entity.TokenHash,
                entity.UserId,
                entity.FamilyId,
                entity.RevokedAtUtc is not null,
                entity.RevokedAtUtc,
                entity.ExpiresAtUtc,
                entity.RevokedReason);
        }
        finally
        {
            await session.DisableGlobalUserLookupAsync(cancellationToken);
        }
    }

    public async Task IssueAsync(
        Guid userId,
        Guid familyId,
        string tokenHash,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        await using var session = await _sessionScope.BeginAsync(cancellationToken);
        await session.EnableGlobalUserLookupAsync(cancellationToken);
        try
        {
            var entity = RefreshToken.Create(
                userId,
                tokenHash,
                familyId,
                lifetime,
                _timeProvider);

            _dbContext.RefreshTokens.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await session.DisableGlobalUserLookupAsync(cancellationToken);
        }
    }

    public async Task RevokeAsync(
        string tokenHash,
        string reason,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (revokedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("El instante de revocación debe ser UTC.", nameof(revokedAtUtc));
        }

        await using var session = await _sessionScope.BeginAsync(cancellationToken);
        await session.EnableGlobalUserLookupAsync(cancellationToken);
        try
        {
            int affected = await _dbContext.RefreshTokens
                .Where(r => r.TokenHash == tokenHash)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.RevokedAtUtc, revokedAtUtc)
                    .SetProperty(r => r.RevokedReason, reason), cancellationToken);

            // ExecuteUpdateAsync no dispara SaveChanges; el cambio ya se aplicó en SQL.
            // No se requiere SaveChangesAsync porque ExecuteUpdateAsync persiste directamente.
        }
        finally
        {
            await session.DisableGlobalUserLookupAsync(cancellationToken);
        }
    }

    public async Task RevokeFamilyAsync(
        Guid familyId,
        string reason,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (revokedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("El instante de revocación debe ser UTC.", nameof(revokedAtUtc));
        }

        await using var session = await _sessionScope.BeginAsync(cancellationToken);
        await session.EnableGlobalUserLookupAsync(cancellationToken);
        try
        {
            await _dbContext.RefreshTokens
                .Where(r => r.FamilyId == familyId && r.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.RevokedAtUtc, revokedAtUtc)
                    .SetProperty(r => r.RevokedReason, reason), cancellationToken);
        }
        finally
        {
            await session.DisableGlobalUserLookupAsync(cancellationToken);
        }
    }

    public async Task<RefreshTokenRotationOutcome> RotateAsync(
        string currentTokenHash,
        string newTokenHash,
        DateTime nowUtc,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentTokenHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(newTokenHash);

        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("El instante debe ser UTC.", nameof(nowUtc));
        }

        await using var session = await _sessionScope.BeginAsync(cancellationToken);
        await session.EnableGlobalUserLookupAsync(cancellationToken);
        try
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Revocación condicional atómica: solo si el token aún no está revocado.
                int revoked = await _dbContext.RefreshTokens
                    .Where(r => r.TokenHash == currentTokenHash && r.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(r => r.RevokedAtUtc, nowUtc)
                        .SetProperty(r => r.RevokedReason, RefreshTokenRevocationReasons.Rotated)
                        .SetProperty(r => r.ReplacedByTokenHash, newTokenHash), cancellationToken);

                if (revoked == 0)
                {
                    // El token ya estaba revocado: se devuelve su estado actual para que
                    // el handler distinga rotación concurrente de reuso real.
                    await transaction.RollbackAsync(cancellationToken);
                    var current = await _dbContext.RefreshTokens
                        .AsNoTracking()
                        .FirstOrDefaultAsync(r => r.TokenHash == currentTokenHash, cancellationToken);

                    RefreshTokenCandidate? currentCandidate = current is null
                        ? null
                        : new RefreshTokenCandidate(
                            current.TokenHash,
                            current.UserId,
                            current.FamilyId,
                            current.RevokedAtUtc is not null,
                            current.RevokedAtUtc,
                            current.ExpiresAtUtc,
                            current.RevokedReason);

                    return new RefreshTokenRotationOutcome(
                        Success: false,
                        CurrentToken: currentCandidate,
                        NewTokenHash: null,
                        UserId: null,
                        FamilyId: null);
                }

                // Recuperar UserId y FamilyId del token revocado (lectura no rastreada).
                var oldToken = await _dbContext.RefreshTokens
                    .AsNoTracking()
                    .FirstAsync(r => r.TokenHash == currentTokenHash, cancellationToken);

                // Insertar el nuevo token en la misma familia.
                var newEntity = RefreshToken.Create(
                    oldToken.UserId,
                    newTokenHash,
                    oldToken.FamilyId,
                    lifetime,
                    _timeProvider);

                _dbContext.RefreshTokens.Add(newEntity);
                await _dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new RefreshTokenRotationOutcome(
                    Success: true,
                    CurrentToken: null,
                    NewTokenHash: newTokenHash,
                    UserId: oldToken.UserId,
                    FamilyId: oldToken.FamilyId);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        finally
        {
            await session.DisableGlobalUserLookupAsync(cancellationToken);
        }
    }
}
