using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.PosDevices.Commands;
using Pos.Application.PosDevices.DTOs;
using Pos.Application.PosDevices.Queries;

namespace Pos.Api.Controllers.Tenant;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PosDevicesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public PosDevicesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet("branch/{branchId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PosDeviceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PosDeviceDto>>> GetPosDevicesByBranch(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var query = new GetPosDevicesByBranchQuery(branchId);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PosDeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPosDeviceById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetPosDeviceByIdQuery(id);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(PosDeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterPosDevice(
        [FromBody] RegisterPosDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterPosDeviceCommand(request.BranchId, request.Name, request.SerialNumber);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/ping")]
    [ProducesResponseType(typeof(PosDeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PingPosDevice(Guid id, CancellationToken cancellationToken)
    {
        var command = new PingPosDeviceCommand(id);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
