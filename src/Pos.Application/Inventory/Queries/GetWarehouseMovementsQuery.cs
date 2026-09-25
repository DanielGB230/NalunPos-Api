using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Queries;

/// <summary>
/// Query: Kardex de movimientos filtrado por almacén (sin requerir productId).
/// Usada por la vista "Kardex" del módulo de inventario.
/// </summary>
[HasPermission(Permissions.Inventory.View)]
public record GetWarehouseMovementsQuery(
    Guid WarehouseId,
    int PageNumber = 1,
    int PageSize = 20
) : IQuery<Result<PagedResult<InventoryMovementDto>>>;

[HasPermission(Permissions.Inventory.View)]
public class GetWarehouseMovementsQueryHandler
    : IQueryHandler<GetWarehouseMovementsQuery, Result<PagedResult<InventoryMovementDto>>>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IWarehouseRepository _warehouseRepository;

    public GetWarehouseMovementsQueryHandler(
        IInventoryRepository inventoryRepository,
        IWarehouseRepository warehouseRepository)
    {
        _inventoryRepository = inventoryRepository
            ?? throw new ArgumentNullException(nameof(inventoryRepository));
        _warehouseRepository = warehouseRepository
            ?? throw new ArgumentNullException(nameof(warehouseRepository));
    }

    public async Task<Result<PagedResult<InventoryMovementDto>>> HandleAsync(
        GetWarehouseMovementsQuery request,
        CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return Result.Fail<PagedResult<InventoryMovementDto>>(
                DomainError.NotFound("Warehouse.NotFound",
                    $"No se encontró el almacén con el ID '{request.WarehouseId}'."));
        }

        var (items, totalCount) = await _inventoryRepository.GetMovementsPagedAsync(
            productId: null,
            warehouseId: request.WarehouseId,
            movementType: null,
            dateFrom: null,
            dateTo: null,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(InventoryMovementDto.FromEntity).ToList();
        var pagedResult = new PagedResult<InventoryMovementDto>(
            dtos, request.PageNumber, request.PageSize, totalCount);

        return Result.Ok(pagedResult);
    }
}
