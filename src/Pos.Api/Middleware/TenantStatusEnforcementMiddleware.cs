using System.Net;
using Microsoft.AspNetCore.Http;
using Pos.Api.Common;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;

namespace Pos.Api.Middleware;

/// <summary>
/// Middleware de aplicación de control de acceso por estado de tenant.
/// Verifica, en cada petición autenticada de usuario de tenant, que el tenant esté
/// en estado <see cref="Pos.Domain.Enums.TenantStatus.Active"/> antes de permitir el acceso.
/// </summary>
/// <remarks>
/// Responsabilidad distinta a <see cref="TenantResolutionMiddleware"/>: éste solo resuelve
/// el contexto; este middleware lo hace cumplir.
///
/// Se registra en el pipeline entre <see cref="TenantResolutionMiddleware"/> y
/// <see cref="Microsoft.AspNetCore.Builder.AuthorizationAppBuilderExtensions.UseAuthorization"/>.
///
/// Solo actúa cuando:
/// <list type="bullet">
/// <item>La petición está autenticada (<see cref="HttpContext.User.Identity.IsAuthenticated"/>).</item>
/// <item>Hay un <c>TenantId</c> resuelto en <see cref="ICurrentTenantContext"/>.</item>
/// <item>El usuario NO es SuperAdmin de plataforma.</item>
/// </list>
///
/// Si la política deniega, responde 403 ProblemDetails con extensión <c>code</c>
/// (vía <see cref="PosProblemDetailsFactory"/>) sin lanzar excepciones como flujo de control.
/// Endpoints anónimos y SuperAdmin no se ven afectados.
///
/// Las dependencias con alcance (scoped) se inyectan como parámetros de <see cref="InvokeAsync"/>
/// (no en el constructor), conforme al patrón de <see cref="TenantResolutionMiddleware"/>.
/// </remarks>
public class TenantStatusEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public TenantStatusEnforcementMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenantContext tenantContext,
        ITenantStatusReader statusReader)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(statusReader);

        // Solo aplica a peticiones autenticadas con tenant y que no son SuperAdmin de plataforma.
        if (context.User.Identity?.IsAuthenticated == true
            && tenantContext.HasTenant
            && !tenantContext.IsSuperAdmin)
        {
            var tenantId = tenantContext.TenantId!.Value;

            var status = await statusReader.GetStatusAsync(tenantId);
            // Solo deniega cuando el tenant EXISTE en BD pero no está activo.
            // Un tenant inexistente (null) no se bloquea aquí para evitar romper
            // flujos legados; el login (LoginCommandHandler) aplica la política de
            // forma estricta (fail-closed: null → Tenant.NotActive).
            if (status is not null)
            {
                var accessError = TenantAccessPolicy.ValidateAccess(status);
                if (accessError is not null)
                {
                    var problemDetails = PosProblemDetailsFactory.CreateProblemDetails(context, accessError);
                    context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";

                    var json = System.Text.Json.JsonSerializer.Serialize(problemDetails);
                    await context.Response.WriteAsync(json, context.RequestAborted);
                    return;
                }
            }
        }

        await _next(context);
    }
}
