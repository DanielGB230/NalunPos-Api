using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.CashRegisters.Commands;
using Pos.Application.CashRegisters.DTOs;
using Pos.Application.CashRegisters.Queries;

namespace Pos.Api.Controllers;

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
        [FromBody] CreateCashRegisterCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetCashRegisters), null, result);
    }

    [HttpPost("sessions/open")]
    [ProducesResponseType(typeof(CashRegisterSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OpenSession(
        [FromBody] OpenCashRegisterSessionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("sessions/close")]
    [ProducesResponseType(typeof(CashRegisterSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseSession(
        [FromBody] CloseCashRegisterSessionCommand command,
        CancellationToken cancellationToken)
    {
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
