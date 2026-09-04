using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Queries;

public record GetProductStockQuery(Guid ProductId) : IQuery<ProductStockDto>;

public class GetProductStockQueryHandler : IQueryHandler<GetProductStockQuery, ProductStockDto>
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

    public async Task<ProductStockDto> HandleAsync(GetProductStockQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new ProductNotFoundException(request.ProductId);

        // Suma Kardex calculada en base de datos
        decimal calculatedStock = await _inventoryRepository.GetCurrentStockAsync(request.ProductId, cancellationToken);

        return new ProductStockDto(product.Id, product.Name, product.Sku.Value, calculatedStock);
    }
}
