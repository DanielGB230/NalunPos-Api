using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Branches.Commands;
using Pos.Application.Branches.DTOs;
using Pos.Application.Branches.Queries;

namespace Pos.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BranchesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public BranchesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetBranches(
        [FromQuery] bool? isActiveOnly = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetBranchesQuery(isActiveOnly);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBranchById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetBranchByIdQuery(id);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBranch(
        [FromBody] CreateBranchCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(BranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBranch(
        Guid id,
        [FromBody] UpdateBranchCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("El ID de la ruta no coincide con el ID del cuerpo.");
        }

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
