using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
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
            request.SessionId,
            request.CustomerId,
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
        [FromBody] CreateSaleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
