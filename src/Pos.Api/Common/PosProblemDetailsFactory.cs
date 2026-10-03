using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Pos.Domain.Common;

namespace Pos.Api.Common;

/// <summary>
/// Fábrica estática única de <see cref="ProblemDetails"/> para la API de NalunPos (RFC 9110 / RFC 9457).
/// Centraliza la asignación de Type, Title, Status, Detail, Instance, CorrelationId y la extensión "errors"
/// formateada en camelCase a partir de un <see cref="FieldErrors"/>.
/// </summary>
public static class PosProblemDetailsFactory
{
    public static string GetCorrelationId(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headerValue = context.Response.Headers["X-Correlation-ID"].ToString();
        if (!string.IsNullOrEmpty(headerValue))
            return headerValue;

        var requestHeaderValue = context.Request.Headers["X-Correlation-ID"].ToString();
        if (!string.IsNullOrEmpty(requestHeaderValue))
            return requestHeaderValue;

        var itemValue = context.Items["CorrelationId"]?.ToString();
        if (!string.IsNullOrEmpty(itemValue))
            return itemValue;

        return context.TraceIdentifier;
    }

    public static ProblemDetails CreateProblemDetails(
        HttpContext context,
        int statusCode,
        string? title = null,
        string? detail = null,
        FieldErrors? fieldErrors = null,
        string? customType = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (defaultType, defaultTitle) = GetDefaultsForStatus(statusCode);

        var problemDetails = new ProblemDetails
        {
            Type = customType ?? defaultType,
            Title = title ?? defaultTitle,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["correlationId"] = GetCorrelationId(context);

        if (fieldErrors != null && fieldErrors.Values.Count > 0)
        {
            var formattedErrors = new Dictionary<string, string[]>(fieldErrors.Values.Count, StringComparer.Ordinal);
            foreach (var (key, value) in fieldErrors.Values)
            {
                var camelKey = ToCamelCasePropertyPath(key);
                formattedErrors[camelKey] = value.ToArray();
            }
            problemDetails.Extensions["errors"] = formattedErrors;
        }

        return problemDetails;
    }

    public static ProblemDetails CreateProblemDetails(
        HttpContext context,
        DomainError error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        var statusCode = error.Type switch
        {
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Validation   => StatusCodes.Status400BadRequest,
            ErrorType.NotFound     => StatusCodes.Status404NotFound,
            ErrorType.Conflict     => StatusCodes.Status409Conflict,
            _                      => StatusCodes.Status500InternalServerError
        };

        var detail = error.Type is ErrorType.Failure
            ? "Ha ocurrido un error inesperado en el servidor. Por favor, consulte los registros del sistema."
            : error.Message;

        return CreateProblemDetails(
            context,
            statusCode,
            detail: detail,
            fieldErrors: error.Errors);
    }

    public static string ToCamelCasePropertyPath(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return string.Empty;

        var segments = propertyName.Split('.');
        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (string.IsNullOrEmpty(segment))
                continue;

            int bracketIndex = segment.IndexOf('[');
            if (bracketIndex > 0)
            {
                var prop = segment[..bracketIndex];
                var bracketPart = segment[bracketIndex..];
                segments[i] = ToCamelCaseSegment(prop) + bracketPart;
            }
            else if (bracketIndex == 0)
            {
                segments[i] = segment;
            }
            else
            {
                segments[i] = ToCamelCaseSegment(segment);
            }
        }

        return string.Join('.', segments);
    }

    private static string ToCamelCaseSegment(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;

        return char.ToLowerInvariant(str[0]) + str[1..];
    }

    private static (string Type, string Title) GetDefaultsForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => (
            "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            "Error de validación"),
        StatusCodes.Status401Unauthorized => (
            "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            "No autorizado"),
        StatusCodes.Status403Forbidden => (
            "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            "Acceso denegado"),
        StatusCodes.Status404NotFound => (
            "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            "Recurso no encontrado"),
        StatusCodes.Status409Conflict => (
            "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            "Conflicto de estado"),
        StatusCodes.Status429TooManyRequests => (
            "https://tools.ietf.org/html/rfc9110#section-15.5.20",
            "Demasiadas solicitudes"),
        _ => (
            "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            "Error interno del servidor")
    };
}
