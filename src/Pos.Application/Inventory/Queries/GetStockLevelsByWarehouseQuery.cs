using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Inventory.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Inventory.Queries;

/// <summary>
/// Query: lista paginada de StockLevel para un almacén dado.
/// Usada por la vista "Stock Actual" del módulo de inventario.
/// </summary>
public record GetStockLevelsByWarehouseQuery(
    Guid WarehouseId,
    int PageNumber = 1,
    int PageSize = 10
) : IQuery<Result<PagedResult<StockLevelDto>>>;

public class GetStockLevelsByWarehouseQueryHandler
    : IQueryHandler<GetStockLevelsByWarehouseQuery, Result<PagedResult<StockLevelDto>>>
{
    private readonly IStockLevelRepository _stockLevelRepository;
    private readonly IWarehouseRepository _warehouseRepository;

    public GetStockLevelsByWarehouseQueryHandler(
        IStockLevelRepository stockLevelRepository,
        IWarehouseRepository warehouseRepository)
    {
        _stockLevelRepository = stockLevelRepository
            ?? throw new ArgumentNullException(nameof(stockLevelRepository));
        _warehouseRepository = warehouseRepository
            ?? throw new ArgumentNullException(nameof(warehouseRepository));
    }

    public async Task<Result<PagedResult<StockLevelDto>>> HandleAsync(
        GetStockLevelsByWarehouseQuery request,
        CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseRepository.GetByIdAsync(request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return Result.Fail<PagedResult<StockLevelDto>>(
                DomainError.NotFound("Warehouse.NotFound",
                    $"No se encontró el almacén con el ID '{request.WarehouseId}'."));
        }

        var allLevels = await _stockLevelRepository.GetByWarehouseAsync(
            request.WarehouseId, cancellationToken);

        // Paginación en memoria (el repositorio no pagina — ADR: colecciones pequeñas por almacén)
        var totalCount = allLevels.Count;
        var paged = allLevels
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(StockLevelDto.FromEntity)
            .ToList();

        var result = new PagedResult<StockLevelDto>(paged, request.PageNumber, request.PageSize, totalCount);
        return Result.Ok(result);
    }
}
