using Pos.Application.Common.Interfaces;
using Pos.Application.Categories.DTOs;
using Pos.Application.Common.Models;
using Pos.Domain.Interfaces;

namespace Pos.Application.Categories.Queries;

/// <summary>
/// Query interna que transporta los parámetros de filtro y paginación al Handler.
/// El controlador construye esta query a partir del GetCategoriesRequest.
/// </summary>
public record GetCategoriesQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    bool? IsActive = null
) : IQuery<PagedResult<CategoryDto>>;

public class GetCategoriesQueryHandler : IQueryHandler<GetCategoriesQuery, PagedResult<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
    }

    public async Task<PagedResult<CategoryDto>> HandleAsync(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _categoryRepository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.IsActive,
            cancellationToken);

        var dtos = items.Select(CategoryDto.FromEntity).ToList();

        return new PagedResult<CategoryDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
