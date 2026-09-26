using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Pos.Api.Middleware;

/// <summary>
/// Middleware encargado de generar o propagar el CorrelationId por cada request entrante.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingCorrelationId) &&
            !string.IsNullOrWhiteSpace(existingCorrelationId))
        {
            correlationId = existingCorrelationId.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }
}
