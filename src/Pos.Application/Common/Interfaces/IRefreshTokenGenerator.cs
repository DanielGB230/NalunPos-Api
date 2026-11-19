namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Puerto de generación de material de refresh token rotativo.
/// Produce el token opaco (claro, solo para transporte a la cookie HttpOnly)
/// y su hash SHA-256 (el único valor que se persiste en BD).
/// </summary>
public sealed record RefreshTokenMaterial(string Token, string TokenHash);

/// <summary>
/// Contrato para generar tokens de actualización (refresh tokens) opacos
/// y sus huellas criptográficas.
/// </summary>
public interface IRefreshTokenGenerator
{
    /// <summary>
    /// Genera un par (token claro, hash SHA-256) para un nuevo refresh token.
    /// El token claro se envía al cliente vía cookie HttpOnly; el hash se persiste.
    /// </summary>
    RefreshTokenMaterial Generate();
}
