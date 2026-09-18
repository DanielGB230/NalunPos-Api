using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.StockAdjustments.Commands;
using Pos.Application.StockAdjustments.DTOs;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/stock-adjustments")]
public class StockAdjustmentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public StockAdjustmentsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpPost]
    [ProducesResponseType(typeof(StockAdjustmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAdjustment(
        [FromBody] CreateStockAdjustmentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
