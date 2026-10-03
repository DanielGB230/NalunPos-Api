using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Api.Options;

namespace Pos.Api.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();

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
                var httpContext = context.HttpContext;
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter) && retryAfter > TimeSpan.Zero)
                {
                    var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    httpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    httpContext.Response.Headers.RetryAfter = (rateLimitingOptions.GlobalApiPolicy.WindowMinutes * 60).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetailsService.WriteProblemDetailsAsync(
                    httpContext,
                    StatusCodes.Status429TooManyRequests,
                    title: "Demasiadas solicitudes",
                    detail: "Se ha superado el límite de peticiones permitido. Intente nuevamente en unos momentos.");
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
