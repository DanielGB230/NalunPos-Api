using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.PurchaseOrders.Commands;
using Pos.Application.PurchaseOrders.DTOs;
using Pos.Application.PurchaseOrders.Queries;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/purchase-orders")]
[EnableRateLimiting("GlobalApiPolicy")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public PurchaseOrdersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(Pos.Application.Common.Models.PagedResult<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPurchaseOrders(
        [FromQuery] GetPurchaseOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPurchaseOrdersQuery(request.PageNumber, request.PageSize, request.SupplierId, request.WarehouseId, request.Status);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PurchaseOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePurchaseOrder(
        [FromBody] CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreatePurchaseOrderCommand(
            request.SupplierId,
            request.WarehouseId,
            request.OrderNumber,
            request.Items.Select(i => new CreatePurchaseOrderLineDto(i.ProductId, i.Quantity, i.UnitCostAmount, i.Currency)).ToList(),
            request.Notes);

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
        [FromBody] ReceivePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReceivePurchaseOrderCommand(
            id,
            request.Lines.Select(l => new ReceivePurchaseOrderLineDto(l.ProductId, l.ReceivedQuantity, l.BatchNumber, l.ExpirationDate)).ToList());
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
