using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Authentication;

/// <summary>
/// Clasificación de un refresh token revocado presentado de nuevo.
/// </summary>
public enum RefreshTokenReuseClassification
{
    /// <summary>
    /// Rotación concurrente: el token fue rotado hace poco y el perdedor de la carrera
    /// no debe revocar la familia ni recibir tokens nuevos.
    /// </summary>
    ConcurrentRotation,

    /// <summary>
    /// Reuso real: el token revocado por cualquier otro motivo (o fuera de la ventana)
    /// revoca toda la familia.
    /// </summary>
    Reuse
}

/// <summary>
/// Política pura de clasificación de tokens revocados (F1.G.5).
/// No consulta relojes ni persistencia: recibe el instante UTC y la ventana de tolerancia.
/// </summary>
public static class RefreshTokenReusePolicy
{
    /// <summary>
    /// Clasifica un token revocado como rotación concurrente o reuso.
    /// Solo <see cref="RefreshTokenRevocationReasons.Rotated"/> dentro de la ventana
    /// (inclusiva, con tolerancia a desfase de reloj) es una rotación concurrente.
    /// Una ventana de cero desactiva la tolerancia.
    /// </summary>
    public static RefreshTokenReuseClassification Classify(
        string? revokedReason,
        DateTime? revokedAtUtc,
        DateTime nowUtc,
        TimeSpan rotationLeeway)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("El instante de clasificación del refresh token debe ser UTC.");
        }

        if (revokedAtUtc is { } revoked && revoked.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("El instante de revocación del refresh token debe ser UTC.");
        }

        // Sin instante, sin motivo Rotated o con la ventana desactivada no hay tolerancia.
        if (revokedAtUtc is null ||
            !string.Equals(revokedReason, RefreshTokenRevocationReasons.Rotated, StringComparison.Ordinal) ||
            rotationLeeway <= TimeSpan.Zero)
        {
            return RefreshTokenReuseClassification.Reuse;
        }

        // Inclusiva en el límite; una diferencia negativa (desfase de reloj) también
        // se considera dentro de la ventana.
        TimeSpan elapsed = nowUtc - revokedAtUtc.Value;
        return elapsed <= rotationLeeway
            ? RefreshTokenReuseClassification.ConcurrentRotation
            : RefreshTokenReuseClassification.Reuse;
    }
}
