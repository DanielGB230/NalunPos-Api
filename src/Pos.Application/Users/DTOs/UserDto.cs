using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Application.Users.DTOs;

public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    Guid RoleId,
    Guid? TenantId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static UserDto FromEntity(User user)
    {
        return new UserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Email.Value,
            user.RoleId,
            user.TenantId,
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc
        );
    }
}
