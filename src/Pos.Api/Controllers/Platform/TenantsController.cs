using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Contracts.Requests;
using Pos.Api.Extensions;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Platform.Tenants.Commands.ActivateTenant;
using Pos.Application.Platform.Tenants.Commands.CreateTenant;
using Pos.Application.Platform.Tenants.Commands.SuspendTenant;
using Pos.Application.Platform.Tenants.DTOs;
using Pos.Application.Platform.Tenants.Queries.GetTenants;
using Pos.Domain.Common;

namespace Pos.Api.Controllers.Platform;

/// <summary>
/// Controlador de Gestión de Platform (Control Plane) para Aprovisionamiento de Tenants.
/// Exclusivo para usuarios con rol SuperAdmin.
/// Ultra-delgado: Delega el 100% de la ejecución a IDispatcher.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/platform/tenants")]
[Authorize(Roles = PlatformRoles.SuperAdmin)]
[EnableRateLimiting("SensitiveOperationsPolicy")]
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
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
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
        return this.ToCreatedResult(result, id => id);
    }

    /// <summary>
    /// Activa o suspende un Tenant existente.
    /// El cuerpo solo admite <c>true</c> (activar) o <c>false</c> (suspender).
    /// </summary>
    /// <param name="id">Identificador del Tenant</param>
    /// <param name="request">Request con el estado destino</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns><c>true</c> si la transición se aplicó correctamente</returns>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ChangeTenantStatus(
        Guid id,
        [FromBody] ChangeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = request.IsActive
            ? await _dispatcher.SendAsync(new ActivateTenantCommand(id), cancellationToken)
            : await _dispatcher.SendAsync(new SuspendTenantCommand(id), cancellationToken);

        return this.ToActionResult(result);
    }
}
