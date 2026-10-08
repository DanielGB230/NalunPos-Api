using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Products.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

namespace Pos.Application.Products.Commands;

[HasPermission(Permissions.Products.Create)]
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

[HasPermission(Permissions.Products.Create)]


[HasPermission(Permissions.Products.Create)]
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
