using Pos.Domain.Entities;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Contrato único para la generación de tokens de autenticación JWT.
/// Emite claims de identidad, roleId y el nombre canónico del rol.
/// Devuelve el token junto con su expiración calculada una sola vez: el claim <c>exp</c> y
/// el <c>ExpiresAtUtc</c> de la respuesta de login provienen del mismo valor.
/// </summary>
public interface ITokenGenerator
{
    AccessTokenResult GenerateToken(User user, string roleName);
}
