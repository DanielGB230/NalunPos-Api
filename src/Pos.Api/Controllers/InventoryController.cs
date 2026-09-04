using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
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
    [ProducesResponseType(typeof(InventoryMovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryMovementDto>> RecordMovement(
        [FromBody] RecordInventoryMovementCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetProductStock), new { productId = result.ProductId }, result);
    }

    [HttpGet("products/{productId:guid}/stock")]
    [ProducesResponseType(typeof(ProductStockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductStockDto>> GetProductStock(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var query = new GetProductStockQuery(productId);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("products/{productId:guid}/movements")]
    [ProducesResponseType(typeof(PagedResult<InventoryMovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<InventoryMovementDto>>> GetInventoryHistory(
        Guid productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetInventoryHistoryQuery(productId, pageNumber, pageSize);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }
}
