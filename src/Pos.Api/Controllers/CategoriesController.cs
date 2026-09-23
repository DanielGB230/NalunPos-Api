using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Categories.Commands;
using Pos.Application.Categories.DTOs;
using Pos.Application.Categories.Queries;
using Pos.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public CategoriesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CategoryDto>>> GetCategories(
        [FromQuery] GetCategoriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetCategoriesQuery(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            request.IsActive);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetCategoryByIdQuery(id);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest("El ID de la ruta no coincide con el ID del cuerpo del mensaje.");
        }

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeCategoryStatus(
        Guid id,
        [FromBody] ChangeStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IsActive)
        {
            var command = new ActivateCategoryCommand(id);
            var result = await _dispatcher.SendAsync(command, cancellationToken);
            return this.ToActionResult(result);
        }
        else
        {
            var command = new DeactivateCategoryCommand(id);
            var result = await _dispatcher.SendAsync(command, cancellationToken);
            return this.ToActionResult(result);
        }
    }
}
