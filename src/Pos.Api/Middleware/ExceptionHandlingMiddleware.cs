using System.Net;
using FluentValidation;
using Pos.Api.Extensions;
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
        var (statusCode, type, title, detail, errors) = exception switch
        {
            UnauthorizedDomainException unauthEx => (
                StatusCodes.Status401Unauthorized,
                "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                "No autorizado",
                unauthEx.Message,
                (IReadOnlyList<string>?)null),

            ForbiddenDomainException forbEx => (
                StatusCodes.Status403Forbidden,
                "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                "Acceso denegado",
                forbEx.Message,
                null),

            ProductNotFoundException or CategoryNotFoundException => (
                StatusCodes.Status404NotFound,
                "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                "Recurso no encontrado",
                exception.Message,
                null),

            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                "Error de validación",
                "Uno o más errores de validación ocurrieron.",
                valEx.Errors.Select(e => e.ErrorMessage).ToList()),

            DomainException domEx => (
                StatusCodes.Status400BadRequest,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                "Violación de regla de negocio",
                domEx.Message,
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                "Error interno del servidor",
                "Ocurrió un error inesperado al procesar la solicitud.",
                null)
        };

        await problemDetailsService.WriteProblemDetailsAsync(
            context,
            statusCode,
            type,
            title,
            detail,
            errors);
    }
}
