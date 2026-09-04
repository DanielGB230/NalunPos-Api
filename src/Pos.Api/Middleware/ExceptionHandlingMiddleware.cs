using System.Net;
using System.Text.Json;
using FluentValidation;
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

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            LogUnhandledException(_logger, ex, ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Ocurrió una excepción no controlada: {Message}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string message);

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, title, errors) = exception switch
        {
            ProductNotFoundException or CategoryNotFoundException => (
                HttpStatusCode.NotFound,
                "Recurso no encontrado",
                new List<string> { exception.Message }),

            ValidationException valEx => (
                HttpStatusCode.BadRequest,
                "Error de validación",
                valEx.Errors.Select(e => e.ErrorMessage).ToList()),

            DomainException domEx => (
                HttpStatusCode.BadRequest,
                "Violación de regla de negocio",
                new List<string> { domEx.Message }),

            _ => (
                HttpStatusCode.InternalServerError,
                "Error interno del servidor",
                new List<string> { "Ocurrió un error inesperado al procesar la solicitud." })
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            status = context.Response.StatusCode,
            title,
            errors,
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(response);
        await context.Response.WriteAsync(json);
    }
}
