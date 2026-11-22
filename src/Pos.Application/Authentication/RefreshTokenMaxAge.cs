namespace Pos.Application.Authentication;

/// <summary>
/// Cálculo puro del Max-Age (en segundos) de la cookie de refresh token a partir
/// del vencimiento efectivo del token emitido. Regla única compartida por el login
/// y la rotación: nunca negativo y con mínimo 1 segundo (F1.G.6 / ADR 0033).
/// </summary>
public static class RefreshTokenMaxAge
{
    private const int MinimumSeconds = 1;

    public static int Compute(DateTime expiresAtUtc, DateTime nowUtc)
    {
        double seconds = (expiresAtUtc - nowUtc).TotalSeconds;
        return seconds < MinimumSeconds ? MinimumSeconds : (int)Math.Ceiling(seconds);
    }
}
