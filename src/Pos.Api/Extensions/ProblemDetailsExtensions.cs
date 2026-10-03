using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Common;
using Pos.Domain.Common;

namespace Pos.Api.Extensions;

public static class ProblemDetailsExtensions
{
    /// <summary>
    /// Único escritor de <see cref="ProblemDetails"/> en la respuesta HTTP usando <see cref="IProblemDetailsService"/>.
    /// </summary>
    public static async Task WriteProblemDetailsAsync(
        this IProblemDetailsService problemDetailsService,
        HttpContext context,
        ProblemDetails problemDetails)
    {
        ArgumentNullException.ThrowIfNull(problemDetailsService);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problemDetails);

        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }

    public static Task WriteProblemDetailsAsync(
        this IProblemDetailsService problemDetailsService,
        HttpContext context,
        int statusCode,
        string? title = null,
        string? detail = null,
        FieldErrors? fieldErrors = null)
    {
        var problemDetails = PosProblemDetailsFactory.CreateProblemDetails(
            context,
            statusCode,
            title,
            detail,
            fieldErrors);

        return problemDetailsService.WriteProblemDetailsAsync(context, problemDetails);
    }
}
