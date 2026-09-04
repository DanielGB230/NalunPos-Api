using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Queries;

public record GetProductStockQuery(Guid ProductId) : IQuery<Result<ProductStockDto>>;

public class GetProductStockQueryHandler : IQueryHandler<GetProductStockQuery, Result<ProductStockDto>>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;

    public GetProductStockQueryHandler(
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<Result<ProductStockDto>> HandleAsync(GetProductStockQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Fail<ProductStockDto>(DomainError.NotFound(
                "Product.NotFound",
                $"No se encontró el producto con ID '{request.ProductId}'."));
        }

        // Suma Kardex calculada en base de datos
        decimal calculatedStock = await _inventoryRepository.GetCurrentStockAsync(request.ProductId, cancellationToken);

        return Result.Ok(new ProductStockDto(product.Id, product.Name, product.Sku.Value, calculatedStock));
    }
}
