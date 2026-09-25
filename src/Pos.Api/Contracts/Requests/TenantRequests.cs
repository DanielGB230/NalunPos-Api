namespace Pos.Api.Contracts.Requests;

public record GetTenantsRequest : PaginationRequest
{
    public string? SearchTerm { get; init; }
    public string? Status { get; init; }
}

public record CreateTenantRequest(
    string Name,
    string DocumentNumber,
    string AdminEmail,
    string AdminPassword
);
