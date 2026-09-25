using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Queries;

[HasPermission(Permissions.Inventory.View)]
public record GetInventoryHistoryQuery(
    Guid ProductId,
    Guid? WarehouseId = null,
    int PageNumber = 1,
    int PageSize = 10
) : IQuery<Result<PagedResult<InventoryMovementDto>>>;

[HasPermission(Permissions.Inventory.View)]
public class GetInventoryHistoryQueryHandler : IQueryHandler<GetInventoryHistoryQuery, Result<PagedResult<InventoryMovementDto>>>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;

    public GetInventoryHistoryQueryHandler(
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository)
    {
        _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<Result<PagedResult<InventoryMovementDto>>> HandleAsync(GetInventoryHistoryQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Fail<PagedResult<InventoryMovementDto>>(DomainError.NotFound("Product.NotFound", $"No se encontró el producto con el ID '{request.ProductId}'."));
        }

        var (items, totalCount) = await _inventoryRepository.GetMovementsPagedAsync(
            request.ProductId,
            request.WarehouseId,
            null,
            null,
            null,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(InventoryMovementDto.FromEntity).ToList();

        var pagedResult = new PagedResult<InventoryMovementDto>(dtos, request.PageNumber, request.PageSize, totalCount);
        return Result.Ok(pagedResult);
    }
}
