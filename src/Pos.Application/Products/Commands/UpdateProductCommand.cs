using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Products.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Products.Commands;

public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Barcode,
    Guid CategoryId,
    bool IsActive
) : ICommand<ProductDto>;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID del producto es requerido.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del producto es requerido.")
            .Length(2, 150).WithMessage("El nombre debe contener entre 2 y 150 caracteres.");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("La categoría es requerida.");
    }
}

public class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductCommandHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ProductDto> HandleAsync(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new ProductNotFoundException(request.Id);

        if (product.CategoryId != request.CategoryId)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
                ?? throw new CategoryNotFoundException(request.CategoryId);

            product.ChangeCategory(category.Id);
        }

        var barcodeVo = !string.IsNullOrWhiteSpace(request.Barcode) ? Barcode.Create(request.Barcode) : null;
        product.UpdateDetails(request.Name, request.Description, barcodeVo);

        if (request.IsActive && !product.IsActive)
        {
            product.Activate();
        }
        else if (!request.IsActive && product.IsActive)
        {
            product.Deactivate();
        }

        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ProductDto.FromEntity(product);
    }
}
