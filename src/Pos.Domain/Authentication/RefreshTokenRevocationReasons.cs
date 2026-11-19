namespace Pos.Domain.Authentication;

/// <summary>
/// Motivos canónicos de revocación de un refresh token.
/// Se persisten como texto acotado para auditoría y detección de reuso.
/// </summary>
public static class RefreshTokenRevocationReasons
{
    public const string Rotated = "Rotated";
    public const string Logout = "Logout";
    public const string ReuseDetected = "ReuseDetected";
    public const string Expired = "Expired";
}
