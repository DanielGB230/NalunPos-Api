using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Pos.Infrastructure.Multitenancy;

namespace Pos.Api.Middleware;

/// <summary>
/// Middleware encargado de extraer y resolver el TenantId a partir de los claims del token JWT,
/// asignándolo al HttpContext.Items de la solicitud actual, propagándolo al scope de logging
/// y fijando el contexto de tenant/SuperAdmin en el scope DI vía <see cref="ITenantSetter"/>.
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

    public async Task InvokeAsync(HttpContext context, ITenantSetter tenantSetter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantSetter);

        string? tenantIdStr = null;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var (tenantId, isSuperAdmin) = TenantClaimsResolver.Resolve(context.User, context.Items);

            if (tenantId.HasValue)
            {
                // Mantener Items["TenantId"] para compatibilidad con código que lo lee directamente.
                context.Items["TenantId"] = tenantId.Value;
                tenantIdStr = tenantId.Value.ToString();
            }

            // Fijar estado explícitamente en el scope DI del request.
            // Esto garantiza que CurrentTenantContext devuelva valores correctos incluso
            // dentro de callbacks ejecutados bajo ExecutionContext.SuppressFlow()
            // (p. ej. HybridCache.GetOrCreateAsync), donde IHttpContextAccessor.HttpContext == null.
            tenantSetter.SetTenantId(tenantId);
            tenantSetter.SetSuperAdmin(isSuperAdmin);
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
