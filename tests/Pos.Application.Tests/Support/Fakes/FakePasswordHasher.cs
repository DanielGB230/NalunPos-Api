namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.ValueObjects;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string ValidPassword { get; set; } = "Password123!";

    public string HashPassword(string password) => $"HASHED_{password}";

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (password == ValidPassword) return true;
        return passwordHash.Equals($"HASHED_{password}", StringComparison.OrdinalIgnoreCase) ||
               passwordHash.Equals($"hashed_{password}", StringComparison.OrdinalIgnoreCase);
    }

    public bool Verify(string password, PasswordHash passwordHash)
    {
        if (password == ValidPassword) return true;
        return passwordHash.Value.Equals($"HASHED_{password}", StringComparison.OrdinalIgnoreCase) ||
               passwordHash.Value.Equals($"hashed_{password}", StringComparison.OrdinalIgnoreCase);
    }
}
