using Microsoft.AspNetCore.Mvc;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Sales.Commands;
using Pos.Application.Sales.DTOs;
using Pos.Application.Sales.Queries;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public SalesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SaleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SaleDto>>> GetSales(
        [FromQuery] GetSalesRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSalesQuery(
            request.PageNumber,
            request.PageSize,
            request.CustomerId,
            request.Status,
            request.StartDate,
            request.EndDate);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SaleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSaleById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetSaleByIdQuery(id);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SaleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSale(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateSaleCommand(
            request.ReceiptNumber,
            request.SessionId,
            request.CustomerId,
            request.Items.Select(i => new CreateSaleItemDto(i.ProductId, i.ProductName, i.Quantity, i.UnitPriceAmount)).ToList(),
            request.TaxRatePercentage,
            request.Currency);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
