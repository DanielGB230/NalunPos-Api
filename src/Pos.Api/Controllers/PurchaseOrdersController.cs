using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.Commands;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Application.PurchaseOrders.Queries;
using Pos.Domain.Enums;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public PurchaseOrdersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] PurchaseOrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPurchaseOrdersQuery(pageNumber, pageSize);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePurchaseOrder(
        [FromBody] CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/send")]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendPurchaseOrder(Guid id, CancellationToken cancellationToken)
    {
        var command = new SendPurchaseOrderCommand(id);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/receive")]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceivePurchaseOrder(
        Guid id,
        [FromBody] List<ReceivePurchaseOrderLineDto> lines,
        CancellationToken cancellationToken)
    {
        var command = new ReceivePurchaseOrderCommand(id, lines);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
