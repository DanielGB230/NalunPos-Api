using System.Security.Cryptography;
using System.Text;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Generador de refresh tokens rotativos.
/// Produce un token opaco de 256 bits (claro, solo para la cookie HttpOnly)
/// y su hash SHA-256 (el único valor que se persiste en BD).
/// </summary>
public class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public RefreshTokenMaterial Generate()
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        string tokenHash = ComputeSha256(token);
        return new RefreshTokenMaterial(token, tokenHash);
    }

    private static string ComputeSha256(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
