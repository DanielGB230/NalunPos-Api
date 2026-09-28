using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pos.Api.Options;

namespace Pos.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var rateLimitingOptions = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Demasiadas peticiones",
                    Detail = "Se ha superado el límite de peticiones permitido. Intente nuevamente en unos momentos.",
                    Instance = context.HttpContext.Request.Path
                };
                await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            };

            // AuthPolicy: 10/min por IP para login/refresh
            options.AddPolicy("AuthPolicy", httpContext =>
            {
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitingOptions.AuthPolicy.PermitLimit,
                        Window = TimeSpan.FromMinutes(rateLimitingOptions.AuthPolicy.WindowMinutes),
                        QueueLimit = rateLimitingOptions.AuthPolicy.QueueLimit
                    });
            });

            // PosCheckoutPolicy: 120/min por TenantId para CreateSale
            options.AddPolicy("PosCheckoutPolicy", httpContext =>
            {
                var tenantId = httpContext.Items["TenantId"]?.ToString() ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "global";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: tenantId,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitingOptions.PosCheckoutPolicy.PermitLimit,
                        Window = TimeSpan.FromMinutes(rateLimitingOptions.PosCheckoutPolicy.WindowMinutes),
                        SegmentsPerWindow = rateLimitingOptions.PosCheckoutPolicy.SegmentsPerWindow,
                        QueueLimit = rateLimitingOptions.PosCheckoutPolicy.QueueLimit
                    });
            });

            // SensitiveOperationsPolicy: 60/min por TenantId para CreateUser, IssueInvoice, etc.
            options.AddPolicy("SensitiveOperationsPolicy", httpContext =>
            {
                var tenantId = httpContext.Items["TenantId"]?.ToString() ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "global";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: tenantId,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitingOptions.SensitiveOperationsPolicy.PermitLimit,
                        Window = TimeSpan.FromMinutes(rateLimitingOptions.SensitiveOperationsPolicy.WindowMinutes),
                        QueueLimit = rateLimitingOptions.SensitiveOperationsPolicy.QueueLimit
                    });
            });

            // GlobalApiPolicy: 300/min por TenantId para el resto
            options.AddPolicy("GlobalApiPolicy", httpContext =>
            {
                var tenantId = httpContext.Items["TenantId"]?.ToString() ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "global";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: tenantId,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitingOptions.GlobalApiPolicy.PermitLimit,
                        Window = TimeSpan.FromMinutes(rateLimitingOptions.GlobalApiPolicy.WindowMinutes),
                        QueueLimit = rateLimitingOptions.GlobalApiPolicy.QueueLimit
                    });
            });
        });

        return services;
    }
}
