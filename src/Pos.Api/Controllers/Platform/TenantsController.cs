using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Platform.Tenants.Commands.CreateTenant;
using Pos.Application.Platform.Tenants.DTOs;
using Pos.Application.Platform.Tenants.Queries.GetTenants;

namespace Pos.Api.Controllers.Platform;

/// <summary>
/// Controlador de Gestión de Platform (Control Plane) para Aprovisionamiento de Tenants.
/// Exclusivo para usuarios con rol SuperAdmin.
/// Ultra-delgado: Delega el 100% de la ejecución a IDispatcher.
/// </summary>
[ApiController]
[Route("api/v1/platform/tenants")]
[Authorize(Roles = "SuperAdmin")]
public class TenantsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public TenantsController(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Obtiene la lista paginada de Tenants registradas en la plataforma.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TenantDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<TenantDto>>> GetTenants(
        [FromQuery] GetTenantsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTenantsQuery(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm);

        var result = await _dispatcher.SendAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Aprovisiona un nuevo Tenant y crea su usuario Administrador inicial en la plataforma.
    /// </summary>
    /// <param name="request">Request con datos del tenant y credenciales del administrador inicial</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>ID (Guid) del nuevo Tenant creado</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateTenantCommand(
            request.Name,
            request.DocumentNumber,
            request.AdminEmail,
            request.AdminPassword);

        var result = await _dispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}
