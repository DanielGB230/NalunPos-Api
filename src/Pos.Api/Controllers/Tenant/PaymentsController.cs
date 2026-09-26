using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Payments.Commands;
using Pos.Application.Payments.DTOs;
using Pos.Application.Payments.Queries;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
[EnableRateLimiting("SensitiveOperationsPolicy")]
public class PaymentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public PaymentsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProcessPayment(
        [FromBody] ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ProcessPaymentCommand(
            request.SaleId,
            request.Amount,
            request.Method,
            request.Currency,
            request.ExternalReference);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
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
