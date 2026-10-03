using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Pos.Api.Extensions;

public static class ProblemDetailsExtensions
{
    public static async Task WriteProblemDetailsAsync(
        this IProblemDetailsService problemDetailsService,
        HttpContext context,
        int statusCode,
        string type,
        string title,
        string detail,
        IReadOnlyList<string>? errors = null,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString()
            ?? context.Response.Headers["X-Correlation-ID"].ToString()
            ?? context.TraceIdentifier;

        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Type = type,
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        if (fieldErrors != null && fieldErrors.Count > 0)
        {
            problemDetails.Extensions["errors"] = fieldErrors;
        }
        else if (errors != null && errors.Count > 0)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }
}
