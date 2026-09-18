using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.StockTransfers.Commands;
using Pos.Application.StockTransfers.DTOs;
using Pos.Application.StockTransfers.Queries;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/stock-transfers")]
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
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetStockTransfersQuery(pageNumber, pageSize);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(StockTransferDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateStockTransferCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
