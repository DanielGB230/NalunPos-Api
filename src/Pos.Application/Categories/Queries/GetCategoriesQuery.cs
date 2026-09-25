using Pos.Application.Categories.DTOs;
using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Domain.Interfaces;

namespace Pos.Application.Categories.Queries;

/// <summary>
/// Query interna que transporta los parámetros de filtro y paginación al Handler.
/// </summary>
[HasPermission(Permissions.Categories.View)]
public record GetCategoriesQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    bool? IsActive = null
) : IQuery<PagedResult<CategoryDto>>;

[HasPermission(Permissions.Categories.View)]
public class GetCategoriesQueryHandler : IQueryHandler<GetCategoriesQuery, PagedResult<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
    }

    public async Task<PagedResult<CategoryDto>> HandleAsync(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        int pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        int pageSize = Math.Min(request.PageSize < 1 ? 20 : request.PageSize, 100);

        var (items, totalCount) = await _categoryRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.SearchTerm,
            request.IsActive,
            cancellationToken);

        var dtos = items.Select(CategoryDto.FromEntity).ToList();

        return new PagedResult<CategoryDto>(dtos, pageNumber, pageSize, totalCount);
    }
}
