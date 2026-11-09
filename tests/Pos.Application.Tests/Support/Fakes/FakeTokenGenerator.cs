namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;

public sealed class FakeTokenGenerator : ITokenGenerator
{
    public string GenerateToken(User user) => "fake-jwt-token-for-user";
    public string GenerateToken(User user, string roleName) => "fake-jwt-token-for-user";
}
