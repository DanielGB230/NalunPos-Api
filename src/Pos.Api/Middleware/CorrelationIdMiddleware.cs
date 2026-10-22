using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Pos.Api.Middleware;

/// <summary>
/// Middleware encargado de generar o propagar el CorrelationId por cada request entrante.
/// Valida que el header X-Correlation-ID entrante tenga un máximo de 64 caracteres
/// y contenga únicamente caracteres alfanuméricos o guiones ([a-zA-Z0-9\-_]).
/// Si es inválido, malicioso o vacío, lo ignora y genera un Guid nuevo.
/// </summary>
public partial class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const int MaxCorrelationIdLength = 64;
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$")]
    private static partial Regex ValidCorrelationIdPattern();

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingCorrelationId) &&
            !string.IsNullOrWhiteSpace(existingCorrelationId))
        {
            var headerValue = existingCorrelationId.ToString();
            if (headerValue.Length <= MaxCorrelationIdLength && ValidCorrelationIdPattern().IsMatch(headerValue))
            {
                correlationId = headerValue;
            }
            else
            {
                correlationId = Guid.NewGuid().ToString();
            }
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
