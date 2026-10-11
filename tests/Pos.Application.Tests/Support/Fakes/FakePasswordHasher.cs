namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.ValueObjects;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) => $"HASHED_{password}";

    public bool VerifyPassword(string password, string passwordHash)
    {
        return passwordHash == $"HASHED_{password}";
    }

    public bool Verify(string password, PasswordHash passwordHash)
    {
        return passwordHash.Value == $"HASHED_{password}";
    }
}
