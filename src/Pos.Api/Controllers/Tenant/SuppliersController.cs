using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Common.Models;
using Pos.Application.Suppliers.Commands;
using Pos.Application.Suppliers.DTOs;
using Pos.Application.Suppliers.Queries;

namespace Pos.Api.Controllers.Tenant;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public SuppliersController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SupplierDto>>> GetSuppliers(
        [FromQuery] GetSuppliersRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSuppliersQuery(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.IsActive);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupplierById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetSupplierByIdQuery(id);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] CreateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSupplier(
        Guid id,
        [FromBody] UpdateSupplierCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("El ID de la ruta no coincide con el ID del cuerpo.");
        }

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeSupplierStatus(
        Guid id,
        [FromBody] ChangeStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IsActive)
        {
            var command = new ActivateSupplierCommand(id);
            var result = await _dispatcher.SendAsync(command, cancellationToken);
            return this.ToActionResult(result);
        }
        else
        {
            var command = new DeactivateSupplierCommand(id);
            var result = await _dispatcher.SendAsync(command, cancellationToken);
            return this.ToActionResult(result);
        }
    }
}
