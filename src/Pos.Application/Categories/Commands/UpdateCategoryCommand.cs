using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Categories.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

using Pos.Domain.Common;

namespace Pos.Application.Categories.Commands;

public record UpdateCategoryCommand(Guid Id, string Name, string? Description) : ICommand<Result<CategoryDto>>;

public class UpdateCategoryCommandValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de la categoría es requerido.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la categoría es requerido.")
            .Length(2, 100).WithMessage("El nombre debe contener entre 2 y 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class UpdateCategoryCommandHandler : ICommandHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
            category.Update(request.Name, request.Description);
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
