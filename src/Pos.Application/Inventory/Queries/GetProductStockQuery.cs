using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Queries;

public record GetProductStockQuery(Guid ProductId, Guid? WarehouseId = null) : IQuery<Result<ProductStockDto>>;

public class GetProductStockQueryHandler : IQueryHandler<GetProductStockQuery, Result<ProductStockDto>>
{
    private readonly IStockLevelRepository _stockLevelRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;

    public GetProductStockQueryHandler(
        IStockLevelRepository stockLevelRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository)
    {
        _stockLevelRepository = stockLevelRepository ?? throw new ArgumentNullException(nameof(stockLevelRepository));
        _warehouseRepository = warehouseRepository ?? throw new ArgumentNullException(nameof(warehouseRepository));
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

        Guid warehouseId = request.WarehouseId ?? Guid.Empty;
        if (warehouseId == Guid.Empty)
        {
            var defaultWarehouse = await _warehouseRepository.GetDefaultAsync(cancellationToken);
            warehouseId = defaultWarehouse?.Id ?? Guid.Empty;
        }

        decimal availableStock = 0m;
        if (warehouseId != Guid.Empty)
        {
            var stockLevel = await _stockLevelRepository.GetAsync(request.ProductId, warehouseId, null, cancellationToken);
            availableStock = stockLevel?.QuantityAvailable ?? 0m;
        }

        return Result.Ok(new ProductStockDto(product.Id, product.Name, product.Sku.Value, availableStock));
    }
}
