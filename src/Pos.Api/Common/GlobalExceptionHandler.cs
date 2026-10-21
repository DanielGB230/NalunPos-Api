using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pos.Api.Extensions;
using Pos.Application.Common.Validation;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Api.Common;

/// <summary>
/// Manejador global de excepciones nativo de ASP.NET Core (.NET 10 / RFC 9457).
/// Reemplaza al middleware artesanal capturando excepciones del pipeline y emitiendo ProblemDetails estructurados.
/// </summary>
public partial class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService ?? throw new ArgumentNullException(nameof(problemDetailsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        LogUnhandledException(_logger, exception, exception.Message);

        var problemDetails = exception switch
        {
            UnauthorizedDomainException unauthEx => PosProblemDetailsFactory.CreateProblemDetails(
                httpContext, StatusCodes.Status401Unauthorized, detail: unauthEx.Message),

            ForbiddenDomainException forbEx => PosProblemDetailsFactory.CreateProblemDetails(
                httpContext, StatusCodes.Status403Forbidden, detail: forbEx.Message),

            ProductNotFoundException or CategoryNotFoundException => PosProblemDetailsFactory.CreateProblemDetails(
                httpContext, StatusCodes.Status404NotFound, detail: exception.Message),

            ValidationException valEx => PosProblemDetailsFactory.CreateProblemDetails(
                httpContext, ValidationResultFactory.CreateError(valEx.Errors)),

            ConcurrencyException concEx => PosProblemDetailsFactory.CreateProblemDetails(
                httpContext, StatusCodes.Status409Conflict, title: "Conflicto de concurrencia", detail: concEx.Message),

            DomainException domEx => PosProblemDetailsFactory.CreateProblemDetails(
                httpContext, StatusCodes.Status400BadRequest, title: "Violación de regla de negocio", detail: domEx.Message),

            _ => PosProblemDetailsFactory.CreateProblemDetails(
                httpContext, StatusCodes.Status500InternalServerError, detail: "Ocurrió un error inesperado al procesar la solicitud.")
        };

        await _problemDetailsService.WriteProblemDetailsAsync(httpContext, problemDetails);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Ocurrió una excepción no controlada en el pipeline: {Message}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string message);
}
