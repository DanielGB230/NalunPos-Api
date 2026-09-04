using Pos.Domain.Entities;

namespace Pos.Application.Roles.DTOs;

public record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions
)
{
    public static RoleDto FromEntity(Role role)
    {
        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.Permissions.ToList()
        );
    }
}
