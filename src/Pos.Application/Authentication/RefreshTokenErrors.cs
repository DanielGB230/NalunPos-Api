using Pos.Domain.Common;

namespace Pos.Application.Authentication;

/// <summary>
/// Errores estables del ciclo de refresh token, referenciables por la capa API
/// sin literales de código duplicados.
/// </summary>
public static class RefreshTokenErrors
{
    public const string InvalidCode = "Auth.RefreshTokenInvalid";

    public const string ConcurrentRotationCode = "Auth.RefreshConcurrent";

    /// <summary>Token inexistente, expirado, revocado por reuso o inválido.</summary>
    public static DomainError Invalid => DomainError.Unauthorized(
        InvalidCode,
        "Token de actualización inválido, expirado o revocado.");

    /// <summary>
    /// El token fue rotado por otra petición dentro de la ventana de tolerancia.
    /// No se revoca la familia ni se emiten tokens: el cliente debe reintentar con
    /// la cookie vigente.
    /// </summary>
    public static DomainError ConcurrentRotation => DomainError.Conflict(
        ConcurrentRotationCode,
        "Rotación concurrente del refresh token. Reintente con el token vigente.");
}
