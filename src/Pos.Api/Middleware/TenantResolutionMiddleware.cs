using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Pos.Api.Middleware;

/// <summary>
/// Middleware encargado de extraer y resolver el TenantId a partir de los claims del token JWT,
/// asignándolo al HttpContext.Items de la solicitud actual y propagándolo al scope de logging.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? tenantIdStr = null;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdClaim = context.User.FindFirst(c =>
                c.Type.Equals("tenantId", StringComparison.OrdinalIgnoreCase) ||
                c.Type.Equals("tenant_id", StringComparison.OrdinalIgnoreCase) ||
                c.Type.Equals("tid", StringComparison.OrdinalIgnoreCase))?.Value;

            if (Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                context.Items["TenantId"] = tenantId;
                tenantIdStr = tenantId.ToString();
            }
        }

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["TenantId"] = tenantIdStr ?? "Global"
        }))
        {
            await _next(context);
        }
    }
}
