using Pos.Application.Common.Interfaces;
using Pos.Application.Categories.DTOs;
using Pos.Application.Common.Models;
using Pos.Domain.Interfaces;

namespace Pos.Application.Categories.Queries;

public record GetCategoriesQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    bool? IsActiveOnly = null,
    bool IncludeInactive = false
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
            request.IsActiveOnly,
            request.IncludeInactive,
            cancellationToken);

        var dtos = items.Select(CategoryDto.FromEntity).ToList();

        return new PagedResult<CategoryDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
