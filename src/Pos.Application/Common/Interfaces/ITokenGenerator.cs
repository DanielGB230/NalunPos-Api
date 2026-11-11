using Pos.Domain.Entities;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Contrato único para la generación de tokens de autenticación JWT.
/// Emite claims de identidad, roleId y el nombre canónico del rol.
/// </summary>
public interface ITokenGenerator
{
    string GenerateToken(User user, string roleName);
}
