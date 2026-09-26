using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Inventory.DTOs;
using Pos.Application.Inventory.Queries;
using Pos.Application.Warehouses.Commands;
using Pos.Application.Warehouses.DTOs;
using Pos.Application.Warehouses.Queries;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/warehouses")]
[EnableRateLimiting("GlobalApiPolicy")]
public class WarehousesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WarehousesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>Lista todos los almacenes activos del tenant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouses(CancellationToken cancellationToken)
    {
        var query = new GetWarehousesQuery();
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Stock actual de todos los productos en un almacén (vista de Stock Actual del Inventario).
    /// </summary>
    [HttpGet("{warehouseId:guid}/stock")]
    [ProducesResponseType(typeof(PagedResult<StockLevelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStockByWarehouse(
        Guid warehouseId,
        [FromQuery] GetWarehouseStockRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetStockLevelsByWarehouseQuery(
            warehouseId,
            request.PageNumber,
            request.PageSize);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Kardex de movimientos de un almacén (historial completo de entradas/salidas/ajustes/traspasos).
    /// </summary>
    [HttpGet("{warehouseId:guid}/movements")]
    [ProducesResponseType(typeof(PagedResult<InventoryMovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWarehouseMovements(
        Guid warehouseId,
        [FromQuery] GetWarehouseMovementsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetWarehouseMovementsQuery(
            warehouseId,
            request.PageNumber,
            request.PageSize);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    /// <summary>Crea un nuevo almacén para el tenant activo.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWarehouse(
        [FromBody] CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateWarehouseCommand(request.BranchId, request.Name, request.Description, request.IsDefault);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
