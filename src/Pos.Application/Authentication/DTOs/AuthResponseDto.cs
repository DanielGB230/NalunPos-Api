using Pos.Application.Users.DTOs;

namespace Pos.Application.Authentication.DTOs;

public record AuthResponseDto(
    string Token,
    UserDto User,
    IReadOnlyList<string> Permissions
);
