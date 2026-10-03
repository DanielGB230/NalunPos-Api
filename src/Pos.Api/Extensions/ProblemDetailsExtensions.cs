using Microsoft.AspNetCore.Http;
using Pos.Api.Common;
using Pos.Domain.Common;

namespace Pos.Api.Extensions;

public static class ProblemDetailsExtensions
{
    public static async Task WriteProblemDetailsAsync(
        this IProblemDetailsService problemDetailsService,
        HttpContext context,
        int statusCode,
        string? title = null,
        string? detail = null,
        FieldErrors? fieldErrors = null,
        string? customType = null)
    {
        context.Response.StatusCode = statusCode;

        var problemDetails = PosProblemDetailsFactory.CreateProblemDetails(
            context,
            statusCode,
            title,
            detail,
            fieldErrors,
            customType);

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }
}
