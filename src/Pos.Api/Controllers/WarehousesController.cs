using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Warehouses.Commands;
using Pos.Application.Warehouses.DTOs;
using Pos.Application.Warehouses.Queries;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehousesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public WarehousesController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WarehouseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouses(CancellationToken cancellationToken)
    {
        var query = new GetWarehousesQuery();
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(WarehouseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWarehouse(
        [FromBody] CreateWarehouseCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
