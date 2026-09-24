using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.AI.Commands;
using Pos.Application.AI.DTOs;
using Pos.Application.AI.Queries;

namespace Pos.Api.Controllers.Tenant;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AiGovernanceController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AiGovernanceController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentActionRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentActionRecordDto>>> GetPendingActions(CancellationToken cancellationToken)
    {
        var query = new GetPendingAgentActionsQuery();
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("propose")]
    [ProducesResponseType(typeof(AgentActionRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ProposeAction(
        [FromBody] ProposeAgentActionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/review")]
    [ProducesResponseType(typeof(AgentActionRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewAction(
        Guid id,
        [FromBody] bool approve,
        CancellationToken cancellationToken)
    {
        var command = new ReviewAgentActionCommand(id, approve);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
