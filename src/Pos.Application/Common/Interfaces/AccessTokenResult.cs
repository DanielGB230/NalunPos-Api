namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Resultado de la generación de un access token JWT.
/// Expone el token emitido y su instante de expiración calculado una sola vez por el generador,
/// de modo que el claim <c>exp</c> y el <c>ExpiresAtUtc</c> de la respuesta de login sean la misma fuente de verdad.
/// </summary>
public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);
