using System.Net;
using FluentValidation;
using Pos.Api.Common;
using Pos.Api.Extensions;
using Pos.Application.Common.Validation;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Api.Middleware;

public partial class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetailsService)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            LogUnhandledException(_logger, ex, ex.Message);
            await HandleExceptionAsync(context, problemDetailsService, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Ocurrió una excepción no controlada: {Message}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string message);

    private static async Task HandleExceptionAsync(HttpContext context, IProblemDetailsService problemDetailsService, Exception exception)
    {
        var problemDetails = exception switch
        {
            UnauthorizedDomainException unauthEx => PosProblemDetailsFactory.CreateProblemDetails(
                context, StatusCodes.Status401Unauthorized, detail: unauthEx.Message),

            ForbiddenDomainException forbEx => PosProblemDetailsFactory.CreateProblemDetails(
                context, StatusCodes.Status403Forbidden, detail: forbEx.Message),

            ProductNotFoundException or CategoryNotFoundException => PosProblemDetailsFactory.CreateProblemDetails(
                context, StatusCodes.Status404NotFound, detail: exception.Message),

            ValidationException valEx => PosProblemDetailsFactory.CreateProblemDetails(
                context, ValidationResultFactory.CreateError(valEx.Errors)),

            DomainException domEx => PosProblemDetailsFactory.CreateProblemDetails(
                context, StatusCodes.Status400BadRequest, title: "Violación de regla de negocio", detail: domEx.Message),

            _ => PosProblemDetailsFactory.CreateProblemDetails(
                context, StatusCodes.Status500InternalServerError, detail: "Ocurrió un error inesperado al procesar la solicitud.")
        };

        await problemDetailsService.WriteProblemDetailsAsync(context, problemDetails);
    }
}
