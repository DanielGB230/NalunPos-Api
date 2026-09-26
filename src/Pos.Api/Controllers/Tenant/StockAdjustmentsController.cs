using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.StockAdjustments.Commands;
using Pos.Application.StockAdjustments.DTOs;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock-adjustments")]
[EnableRateLimiting("SensitiveOperationsPolicy")]
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
        [FromBody] CreateStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateStockAdjustmentCommand(
            request.WarehouseId,
            request.Reason,
            request.Items.Select(i => new CreateStockAdjustmentLineDto(i.ProductId, i.Quantity)).ToList(),
            request.Notes);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
