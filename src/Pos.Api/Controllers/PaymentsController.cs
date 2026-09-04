using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Payments.Commands;
using Pos.Application.Payments.DTOs;
using Pos.Application.Payments.Queries;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public PaymentsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentDto>> ProcessPayment(
        [FromBody] ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetPaymentsBySaleId), new { saleId = result.SaleId }, result);
    }

    [HttpGet("sale/{saleId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetPaymentsBySaleId(
        Guid saleId,
        CancellationToken cancellationToken)
    {
        var query = new GetPaymentsBySaleIdQuery(saleId);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }
}
