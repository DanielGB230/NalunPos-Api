namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Puerto de revocación masiva de las sesiones (refresh tokens) de un usuario.
/// Devuelve la cantidad de tokens activos que quedaron revocados (ADR 0034).
/// </summary>
public interface IUserSessionRevoker
{
    /// <summary>
    /// Revoca todos los refresh tokens activos del usuario con el motivo indicado.
    /// La operación es idempotente: los tokens ya revocados no se alteran.
    /// </summary>
    Task<int> RevokeAllAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);
}
