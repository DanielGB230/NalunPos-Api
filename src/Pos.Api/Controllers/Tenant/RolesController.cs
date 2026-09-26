using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Roles.Commands;
using Pos.Application.Roles.DTOs;
using Pos.Application.Roles.Queries;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/roles")]
[EnableRateLimiting("SensitiveOperationsPolicy")]
public class RolesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public RolesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var query = new GetRolesQuery();
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRoleCommand(request.Name, request.Description, request.Permissions);
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
