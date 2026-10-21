using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Api.Extensions;

/// <summary>
/// Health Check de readiness que verifica conectividad real de PosDbContext contra la base de datos (H13).
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly PosDbContext _dbContext;

    public DatabaseHealthCheck(PosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database connection is healthy")
                : HealthCheckResult.Unhealthy("Database connection failed");
        }
        catch (System.Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed with exception", ex);
        }
    }
}

/// <summary>
/// Configuración y mapeo de Health Checks nativos de ASP.NET Core (.NET 10).
/// Resuelve el hallazgo H13 con separación explícita de liveness y readiness.
/// </summary>
public static class HealthCheckExtensions
{
    public static IServiceCollection AddCustomHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Process is live"), tags: ["live"])
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    public static IEndpointRouteBuilder MapCustomHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        }).AllowAnonymous();

        return endpoints;
    }
}
