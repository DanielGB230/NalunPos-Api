namespace Pos.Api.Contracts.Requests;

public record GetUsersRequest : PaginationRequest
{
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    Guid RoleId,
    Guid? TenantId = null
);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    Guid RoleId,
    Guid? TenantId = null
);
