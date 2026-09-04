using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Pos.Api.Middleware;

/// <summary>
/// Middleware encargado de extraer y resolver el TenantId a partir de los claims del token JWT,
/// asignándolo al HttpContext.Items de la solicitud actual.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst("tid")?.Value
                                ?? context.User.FindFirst("TenantId")?.Value;

            if (Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                context.Items["TenantId"] = tenantId;
            }
        }

        await _next(context);
    }
}
