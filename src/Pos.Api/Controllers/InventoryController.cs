using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.Common.Models;
using Pos.Application.Inventory.Commands;
using Pos.Application.Inventory.DTOs;
using Pos.Application.Inventory.Queries;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public InventoryController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpPost("movements")]
    [ProducesResponseType(typeof(InventoryMovementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordMovement(
        [FromBody] RecordInventoryMovementCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("products/{productId:guid}/stock")]
    [ProducesResponseType(typeof(ProductStockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductStock(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var query = new GetProductStockQuery(productId);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("products/{productId:guid}/movements")]
    [ProducesResponseType(typeof(PagedResult<InventoryMovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInventoryHistory(
        Guid productId,
        [FromQuery] GetInventoryHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetInventoryHistoryQuery(
            productId,
            request.WarehouseId,
            request.PageNumber,
            request.PageSize);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }
}
