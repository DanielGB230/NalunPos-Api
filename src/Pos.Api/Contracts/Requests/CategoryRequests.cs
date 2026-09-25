namespace Pos.Api.Contracts.Requests;

public record GetCategoriesRequest : PaginationRequest
{
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

public record CreateCategoryRequest(
    string Name,
    string? Description
);

public record UpdateCategoryRequest(
    string Name,
    string? Description
);
