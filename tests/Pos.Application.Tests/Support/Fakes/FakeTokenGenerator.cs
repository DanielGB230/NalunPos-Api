namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;

public sealed class FakeTokenGenerator : ITokenGenerator
{
    /// <summary>
    /// Expiración fija y distinguible para demostrar que el handler usa exactamente
    /// el valor devuelto por el generador y no calcula ninguna expiración propia.
    /// </summary>
    public static readonly DateTime FixedExpiresAtUtc = new(2037, 5, 6, 7, 8, 9, DateTimeKind.Utc);

    public AccessTokenResult GenerateToken(User user, string roleName) =>
        new("fake-jwt-token-for-user", FixedExpiresAtUtc);
}
