namespace Pos.Api.Contracts.Requests;

public record CreateRoleRequest(
    string Name,
    string? Description = null,
    List<string>? Permissions = null
);
