using Pos.Domain.Entities;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Contrato para la generación de tokens de autenticación JWT.
/// Implementación concreta en Pos.Infrastructure (Parte 4).
/// </summary>
public interface ITokenGenerator
{
    string GenerateToken(User user);
}
