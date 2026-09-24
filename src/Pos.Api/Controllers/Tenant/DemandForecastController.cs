using Pos.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Extensions;
using Pos.Application.AI.DTOs;
using Pos.Application.AI.Queries;

namespace Pos.Api.Controllers.Tenant;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DemandForecastController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public DemandForecastController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    [HttpGet("{productId:guid}")]
    [ProducesResponseType(typeof(DemandForecastDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDemandForecast(
        Guid productId,
        [FromQuery] int daysAhead = 30,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDemandForecastQuery(productId, daysAhead);
        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }
}
