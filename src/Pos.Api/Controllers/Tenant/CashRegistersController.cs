using Microsoft.AspNetCore.Mvc;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.CashRegisters.Commands;
using Pos.Application.CashRegisters.DTOs;
using Pos.Application.CashRegisters.Queries;
using Pos.Application.Common.Interfaces;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[Route("api/[controller]")]
public class CashRegistersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public CashRegistersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CashRegisterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CashRegisterDto>>> GetCashRegisters(CancellationToken cancellationToken)
    {
        var query = new GetCashRegistersQuery();
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CashRegisterDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CashRegisterDto>> CreateCashRegister(
        [FromBody] CreateCashRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateCashRegisterCommand(request.Name, request.SerialNumber);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetCashRegisters), null, result);
    }

    [HttpPost("{registerId:guid}/sessions/open")]
    [ProducesResponseType(typeof(CashRegisterSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OpenSession(
        Guid registerId,
        [FromBody] OpenCashRegisterSessionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new OpenCashRegisterSessionCommand(registerId, request.UserId, request.InitialAmount, request.Currency, request.Notes);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("sessions/{sessionId:guid}/close")]
    [ProducesResponseType(typeof(CashRegisterSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseSession(
        Guid sessionId,
        [FromBody] CloseCashRegisterSessionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CloseCashRegisterSessionCommand(sessionId, request.ActualFinalAmount, request.ExpectedFinalAmount, request.Currency, request.Notes);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{registerId:guid}/active-session")]
    [ProducesResponseType(typeof(CashRegisterSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashRegisterSessionDto>> GetActiveSession(
        Guid registerId,
        CancellationToken cancellationToken)
    {
        var query = new GetActiveSessionQuery(registerId);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        if (result == null) return NotFound("No existe una sesión activa abierta para esta caja.");
        return Ok(result);
    }
}
