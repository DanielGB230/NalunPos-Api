using Pos.Domain.ValueObjects;

namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Contrato para el hashing y verificación de contraseñas de usuarios.
/// Implementación concreta en Pos.Infrastructure (Argon2id, Parte 4).
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    bool Verify(string password, PasswordHash passwordHash);
}
