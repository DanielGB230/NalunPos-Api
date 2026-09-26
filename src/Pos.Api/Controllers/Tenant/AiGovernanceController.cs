using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.AI.Commands;
using Pos.Application.AI.DTOs;
using Pos.Application.AI.Queries;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;

namespace Pos.Api.Controllers.Tenant;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ai-governance")]
[EnableRateLimiting("GlobalApiPolicy")]
public class AiGovernanceController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AiGovernanceController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(PagedResult<AgentActionRecordDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AgentActionRecordDto>>> GetPendingActions(
        [FromQuery] GetPendingActionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetPendingAgentActionsQuery(request.PageNumber, request.PageSize);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("propose")]
    [ProducesResponseType(typeof(AgentActionRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ProposeAction(
        [FromBody] ProposeAgentActionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ProposeAgentActionCommand(
            request.AgentId,
            request.ProposedActionType,
            request.PayloadJson,
            request.RiskLevel);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/review")]
    [ProducesResponseType(typeof(AgentActionRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewAction(
        Guid id,
        [FromBody] ReviewAgentActionRequest request,
        CancellationToken cancellationToken)
    {
        bool approve = string.Equals(request.Action, "Approve", StringComparison.OrdinalIgnoreCase);
        var command = new ReviewAgentActionCommand(id, approve);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
