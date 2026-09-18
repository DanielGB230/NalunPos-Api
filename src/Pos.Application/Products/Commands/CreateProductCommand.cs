using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.Products.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Products.Commands;

public record CreateProductCommand(
    string Name,
    string Sku,
    decimal PriceAmount,
    string Currency,
    Guid CategoryId,
    string? Description = null,
    string? Barcode = null,
    decimal? CostAmount = null,
    int InitialStock = 0
) : ICommand<Result<ProductDto>>;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del producto es requerido.")
            .Length(2, 150).WithMessage("El nombre debe contener entre 2 y 150 caracteres.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("El SKU es requerido.")
            .Length(3, 30).WithMessage("El SKU debe contener entre 3 y 30 caracteres.");

        RuleFor(x => x.PriceAmount)
            .GreaterThanOrEqualTo(0).WithMessage("El precio debe ser mayor o igual a 0.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("La divisa es requerida.")
            .Length(3).WithMessage("La divisa debe ser un código ISO de 3 caracteres (ej: USD).");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("La categoría es requerida.");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("La descripción no puede exceder 1000 caracteres.");

        RuleFor(x => x.Barcode)
            .MaximumLength(50).WithMessage("El código de barras no puede exceder 50 caracteres.");

        RuleFor(x => x.InitialStock)
            .GreaterThanOrEqualTo(0).WithMessage("El stock inicial no puede ser negativo.");
    }
}

public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<ProductDto>> HandleAsync(CreateProductCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category == null)
        {
            return Result.Fail<ProductDto>(DomainError.NotFound(
                "Category.NotFound",
                $"No se encontró la categoría con ID '{request.CategoryId}'."));
        }

        var skuVo = Sku.Create(request.Sku);
        bool skuExists = await _productRepository.ExistsBySkuAsync(skuVo, null, cancellationToken);
        if (skuExists)
        {
            return Result.Fail<ProductDto>(DomainError.Conflict(
                "Product.SkuAlreadyExists",
                $"Ya existe un producto registrado con el SKU '{request.Sku}'."));
        }

        Product product;
        try
        {
            var priceVo = Money.Create(request.PriceAmount, request.Currency);
            var costVo = request.CostAmount.HasValue ? Money.Create(request.CostAmount.Value, request.Currency) : null;
            var barcodeVo = !string.IsNullOrWhiteSpace(request.Barcode) ? Barcode.Create(request.Barcode) : null;

            product = Product.Create(
                request.Name,
                skuVo,
                priceVo,
                request.CategoryId,
                request.Description,
                barcodeVo,
                costVo);
        }
        catch (DomainException ex)
        {
            return Result.Fail<ProductDto>(DomainError.Validation("Product.Invalid", ex.Message));
        }

        await _productRepository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(ProductDto.FromEntity(product));
    }
}
