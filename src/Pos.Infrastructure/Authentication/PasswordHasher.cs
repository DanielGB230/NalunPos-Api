using Pos.Application.Common.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    public bool Verify(string password, PasswordHash passwordHash)
    {
        if (passwordHash is null)
        {
            return false;
        }

        return VerifyPassword(password, passwordHash.Value);
    }
}
