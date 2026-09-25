using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Products.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Products.Commands;

[HasPermission(Permissions.Products.Update)]
public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Barcode,
    Guid CategoryId
) : ICommand<Result<ProductDto>>;

[HasPermission(Permissions.Products.Update)]
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

[HasPermission(Permissions.Products.Update)]
public class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, Result<ProductDto>>
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

    public async Task<Result<ProductDto>> HandleAsync(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product == null)
        {
            return Result.Fail<ProductDto>(DomainError.NotFound(
                "Product.NotFound",
                $"No se encontró el producto con ID '{request.Id}'."));
        }

        if (product.CategoryId != request.CategoryId)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
            if (category == null)
            {
                return Result.Fail<ProductDto>(DomainError.NotFound(
                    "Category.NotFound",
                    $"No se encontró la categoría con ID '{request.CategoryId}'."));
            }

            product.ChangeCategory(category.Id);
        }

        try
        {
            var barcodeVo = !string.IsNullOrWhiteSpace(request.Barcode) ? Barcode.Create(request.Barcode) : null;
            product.UpdateDetails(request.Name, request.Description, barcodeVo);
        }
        catch (DomainException ex)
        {
            return Result.Fail<ProductDto>(DomainError.Validation("Product.Invalid", ex.Message));
        }

        _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(ProductDto.FromEntity(product));
    }
}
