using Pos.Domain.Entities;

namespace Pos.Application.Common.Interfaces;

public interface IJwtTokenGenerator : ITokenGenerator
{
    string GenerateToken(User user, Role role);
}
