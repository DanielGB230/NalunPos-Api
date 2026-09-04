using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Categories.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Categories.Commands;

public record CreateCategoryCommand(string Name, string? Description) : ICommand<CategoryDto>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la categoría es requerido.")
            .Length(2, 100).WithMessage("El nombre debe contener entre 2 y 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede exceder 500 caracteres.");
    }
}

public class CreateCategoryCommandHandler : ICommandHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CategoryDto> HandleAsync(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        bool nameExists = await _categoryRepository.ExistsByNameAsync(request.Name, null, cancellationToken);
        if (nameExists)
        {
            throw new DomainException($"Ya existe una categoría con el nombre '{request.Name}'.");
        }

        var category = Category.Create(request.Name, request.Description);

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CategoryDto.FromEntity(category);
    }
}
