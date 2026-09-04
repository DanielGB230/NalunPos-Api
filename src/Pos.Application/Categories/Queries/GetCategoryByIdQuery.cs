using Pos.Application.Common.Interfaces;
using Pos.Application.Categories.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Categories.Queries;

public record GetCategoryByIdQuery(Guid Id) : IQuery<CategoryDto>;

public class GetCategoryByIdQueryHandler : IQueryHandler<GetCategoryByIdQuery, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;

    public GetCategoryByIdQueryHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
    }

    public async Task<CategoryDto> HandleAsync(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CategoryNotFoundException(request.Id);

        return CategoryDto.FromEntity(category);
    }
}
