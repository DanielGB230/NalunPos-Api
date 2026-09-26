using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.StockTransfers.Commands;
using Pos.Application.StockTransfers.DTOs;
using Pos.Application.StockTransfers.Queries;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/stock-transfers")]
[EnableRateLimiting("SensitiveOperationsPolicy")]
public class StockTransfersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public StockTransfersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StockTransferDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStockTransfers(
        [FromQuery] GetStockTransfersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetStockTransfersQuery(request.PageNumber, request.PageSize, request.SourceWarehouseId, request.DestinationWarehouseId);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StockTransferDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateStockTransferRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateStockTransferCommand(
            request.SourceWarehouseId,
            request.DestinationWarehouseId,
            request.Items.Select(i => new CreateStockTransferLineDto(i.ProductId, i.Quantity)).ToList(),
            request.Notes);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
