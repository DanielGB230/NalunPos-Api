using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Categories.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

using Pos.Domain.Common;

namespace Pos.Application.Categories.Commands;

[RequiresTenant]
[HasPermission(Permissions.Categories.Update)]
public record UpdateCategoryCommand(Guid Id, string Name, string? Description) : ICommand<Result<CategoryDto>>;

public class UpdateCategoryCommandHandler : ICommandHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public UpdateCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<CategoryDto>> HandleAsync(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category == null)
        {
            return Result.Fail<CategoryDto>(DomainError.NotFound("Category.NotFound", $"No se encontró la categoría con el ID '{request.Id}'."));
        }

        bool nameExists = await _categoryRepository.ExistsByNameAsync(request.Name, request.Id, cancellationToken);
        if (nameExists)
        {
            return Result.Fail<CategoryDto>(DomainError.Conflict("Category.AlreadyExists", $"Ya existe otra categoría registrada con el nombre '{request.Name}'."));
        }

        try
        {
            category.Update(request.Name, request.Description, _timeProvider);
        }
        catch (DomainException ex)
        {
            return Result.Fail<CategoryDto>(DomainError.Validation("Category.Invalid", ex.Message));
        }

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(CategoryDto.FromEntity(category));
    }
}
