namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Resultado de una rotación atómica de refresh token.
/// Cuando <see cref="Success"/> es <c>false</c>, <see cref="CurrentToken"/> expone el
/// estado actual del token (o <c>null</c> si no existe) para que el llamador aplique
/// la política de reuso/rotación concurrente.
/// </summary>
public sealed record RefreshTokenRotationOutcome(
    bool Success,
    RefreshTokenCandidate? CurrentToken,
    string? NewTokenHash,
    Guid? UserId,
    Guid? FamilyId);

/// <summary>
/// Candidato de refresh token recuperado por hash, usado para decidir
/// si se rota, se rechaza o se revoca la familia por reuso.
/// </summary>
public sealed record RefreshTokenCandidate(
    string TokenHash,
    Guid UserId,
    Guid FamilyId,
    bool IsRevoked,
    DateTime? RevokedAtUtc,
    DateTime ExpiresAtUtc,
    string? RevokedReason);

/// <summary>
/// Puerto de persistencia de refresh tokens rotativos.
/// Toda operación ejecuta sobre <see cref="Pos.Domain.Authentication.RefreshToken"/>
/// (hash SHA-256, nunca token claro) y respeta el alcance de sesión de
/// autenticación (<see cref="Pos.Infrastructure.Authentication.IAuthenticationSessionScope"/>).
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Busca un refresh token por su hash SHA-256.</summary>
    Task<RefreshTokenCandidate?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emite un nuevo refresh token para un usuario y familia dados.
    /// </summary>
    Task IssueAsync(Guid userId, Guid familyId, string tokenHash, TimeSpan lifetime, CancellationToken cancellationToken = default);

    /// <summary>Revoca un refresh token por hash (una sola vez, idempotente).</summary>
    Task RevokeAsync(string tokenHash, string reason, DateTime revokedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Revoca toda una familia de refresh tokens (detección de reuso).</summary>
    Task RevokeFamilyAsync(Guid familyId, string reason, DateTime revokedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rota atómicamente: revoca el token actual (si aún no está revocado) e inserta
    /// el nuevo en la misma familia, todo en una misma transacción.
    /// Si el token actual ya estaba revocado, retorna <c>Success=false</c> con su
    /// estado actual en <see cref="RefreshTokenRotationOutcome.CurrentToken"/>.
    /// </summary>
    Task<RefreshTokenRotationOutcome> RotateAsync(
        string currentTokenHash,
        string newTokenHash,
        DateTime nowUtc,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);
}
