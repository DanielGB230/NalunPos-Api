using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Pos.Application.Common.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Implementación de IPasswordHasher utilizando Argon2id (ganador PHC, recomendación OWASP 2026).
/// Produce cadenas formateadas PHC que incluyen versión, parámetros, salt y hash para su verificación.
/// </summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int DegreeOfParallelism = 1;
    private const int Iterations = 3;
    private const int MemorySize = 65536; // 64 MB

    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = KeyDerivation(password, salt);

        string saltBase64 = Convert.ToBase64String(salt);
        string hashBase64 = Convert.ToBase64String(hash);

        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}${saltBase64}${hashBase64}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            if (passwordHash.StartsWith("$argon2id$", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = passwordHash.Split('$');
                if (parts.Length != 6)
                {
                    return false;
                }

                byte[] salt = Convert.FromBase64String(parts[4]);
                byte[] expectedHash = Convert.FromBase64String(parts[5]);

                byte[] computedHash = KeyDerivation(password, salt);

                return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
            }

            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }

    public bool Verify(string password, PasswordHash passwordHash)
    {
        if (passwordHash is null)
        {
            return false;
        }

        return VerifyPassword(password, passwordHash.Value);
    }

    private static byte[] KeyDerivation(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySize
        };

        return argon2.GetBytes(HashSize);
    }
}
