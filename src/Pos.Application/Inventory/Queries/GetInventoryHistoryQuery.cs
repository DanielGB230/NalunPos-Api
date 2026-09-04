using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Queries;

public record GetInventoryHistoryQuery(
    Guid ProductId,
    int PageNumber = 1,
    int PageSize = 10
) : IQuery<PagedResult<InventoryMovementDto>>;

public class GetInventoryHistoryQueryHandler : IQueryHandler<GetInventoryHistoryQuery, PagedResult<InventoryMovementDto>>
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

    public async Task<PagedResult<InventoryMovementDto>> HandleAsync(GetInventoryHistoryQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new ProductNotFoundException(request.ProductId);

        var (items, totalCount) = await _inventoryRepository.GetMovementsHistoryPagedAsync(
            request.ProductId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(InventoryMovementDto.FromEntity).ToList();

        return new PagedResult<InventoryMovementDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
