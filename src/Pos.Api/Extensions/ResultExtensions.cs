using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Pos.Api.Common;
using Pos.Application.Authentication.Commands.Login;
using Pos.Domain.Common;

namespace Pos.Api.Extensions;

/// <summary>
/// Métodos de extensión para convertir objetos Result / Result&lt;T&gt; a ActionResult de ASP.NET Core MVC.
/// Mapea de forma transparente los tipos de ErrorType a códigos de estado HTTP semánticos.
/// </summary>
public static class ResultExtensions
{
    public static ActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(result.Value);
        }

        return MapErrorToActionResult(controller, result.Error);
    }

    public static ActionResult ToActionResult<TSource, TDestination>(
        this ControllerBase controller,
        Result<TSource> result,
        Func<TSource, TDestination> mapper)
    {
        if (result.IsSuccess)
        {
            return controller.Ok(mapper(result.Value));
        }

        return MapErrorToActionResult(controller, result.Error);
    }

    /// <summary>
    /// Convierte un Result&lt;T&gt; a 201 Created con cabecera Location calculada a partir de routeValues o URI absoluta/relativa.
    /// </summary>
    public static ActionResult ToCreatedResult<T>(
        this ControllerBase controller,
        Result<T> result,
        string? actionName,
        object? routeValues)
    {
        if (result.IsSuccess)
        {
            return controller.CreatedAtAction(actionName, routeValues, result.Value);
        }

        return MapErrorToActionResult(controller, result.Error);
    }

    /// <summary>
    /// Convierte un Result&lt;T&gt; a 201 Created calculando el route value id vía un selector seguro sobre la entidad sin acceder a result.Value directamente en el controller.
    /// </summary>
    public static ActionResult ToCreatedResult<T>(
        this ControllerBase controller,
        Result<T> result,
        string actionName,
        Func<T, object?> idSelector)
    {
        if (result.IsSuccess)
        {
            var id = idSelector(result.Value);
            return controller.CreatedAtAction(actionName, new { id }, result.Value);
        }

        return MapErrorToActionResult(controller, result.Error);
    }

    /// <summary>
    /// Convierte un Result&lt;T&gt; a 201 Created con cabecera Location explícita basada en URI.
    /// </summary>
    public static ActionResult ToCreatedResult<T>(
        this ControllerBase controller,
        Result<T> result,
        string uri)
    {
        if (result.IsSuccess)
        {
            return controller.Created(uri, result.Value);
        }

        return MapErrorToActionResult(controller, result.Error);
    }

    /// <summary>
    /// Convierte un Result&lt;T&gt; a 201 Created deduciendo la propiedad Id del payload para la cabecera Location.
    /// </summary>
    public static ActionResult ToCreatedResult<T>(
        this ControllerBase controller,
        Result<T> result,
        Func<T, object?> idSelector)
    {
        if (result.IsSuccess)
        {
            var id = idSelector(result.Value);
            var path = controller.Request?.Path.Value?.TrimEnd('/') ?? string.Empty;
            var location = id != null ? $"{path}/{id}" : path;
            return controller.Created(location, result.Value);
        }

        return MapErrorToActionResult(controller, result.Error);
    }

    public static ActionResult ToActionResult(this ControllerBase controller, Result result)
    {
        if (result.IsSuccess)
        {
            return controller.Ok();
        }

        return MapErrorToActionResult(controller, result.Error);
    }

    /// <summary>
    /// Convierte un <see cref="Result{LoginResponse}"/> de login/refresh en 200 OK con la
    /// respuesta plana y escribe la cookie HttpOnly <c>nalunpos_rt</c>. En caso de error
    /// retorna el ProblemDetails usando <see cref="IProblemDetailsService"/> (content-type
    /// <c>application/problem+json</c>). La cookie solo se borra cuando
    /// <see cref="RefreshTokenCookiePolicy.ShouldClear"/> lo indica
    /// (<c>Auth.RefreshTokenInvalid</c>); los errores recuperables la conservan (ADR 0032).
    /// </summary>
    public static async Task<ActionResult> ToAuthResultAsync(this ControllerBase controller, Result<LoginResponse> result)
    {
        var cookieWriter = controller.HttpContext.RequestServices.GetRequiredService<RefreshTokenCookieWriter>();

        if (result.IsSuccess)
        {
            var response = result.Value;
            cookieWriter.Write(
                controller.Response,
                response.RefreshToken!,
                response.RefreshTokenMaxAgeSeconds);
            return controller.Ok(response);
        }

        // La cookie solo se limpia cuando la sesión murió definitivamente
        // (Auth.RefreshTokenInvalid). Ver ADR 0032 y RefreshTokenCookiePolicy.
        if (RefreshTokenCookiePolicy.ShouldClear(result.Error))
        {
            cookieWriter.Clear(controller.Response);
        }
        var problemDetailsService = controller.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        var statusCode = result.Error.Type switch
        {
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        controller.HttpContext.Response.StatusCode = statusCode;
        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = statusCode,
            Title = GetProblemDetailsTitle(result.Error.Type),
            Detail = result.Error.Message,
            Type = GetProblemDetailsType(result.Error.Type),
            Instance = controller.HttpContext.Request.Path
        };
        problemDetails.Extensions["code"] = result.Error.Code;
        problemDetails.Extensions["correlationId"] = PosProblemDetailsFactory.GetCorrelationId(controller.HttpContext);
        if (result.Error.Errors != null && result.Error.Errors.Values.Count > 0)
        {
            problemDetails.Extensions["errors"] = result.Error.Errors.Values.ToDictionary(
                kvp => kvp.Key,
                kvp => (object?)kvp.Value.ToArray());
        }

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = controller.HttpContext,
            ProblemDetails = problemDetails
        });

        return new EmptyResult();
    }

    private static string GetProblemDetailsTitle(ErrorType errorType) => errorType switch
    {
        ErrorType.Unauthorized => "No autorizado",
        ErrorType.Forbidden => "Acceso denegado",
        ErrorType.Validation => "Error de validación",
        ErrorType.NotFound => "Recurso no encontrado",
        ErrorType.Conflict => "Conflicto de estado",
        _ => "Error interno del servidor"
    };

    private static string GetProblemDetailsType(ErrorType errorType) => errorType switch
    {
        ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        ErrorType.Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        ErrorType.Validation => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        ErrorType.NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        ErrorType.Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
    };

    /// <summary>
    /// Convierte un <see cref="Result"/> sin valor en 204 No Content (éxito) o en el
    /// ProblemDetails correspondiente (error).
    /// </summary>
    public static ActionResult ToNoContentResult(this ControllerBase controller, Result result)
    {
        if (result.IsSuccess)
        {
            return controller.NoContent();
        }

        return MapErrorToActionResult(controller, result.Error);
    }

    private static ObjectResult MapErrorToActionResult(ControllerBase controller, DomainError error)
    {
        var problemDetails = PosProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, error);
        var objectResult = new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status,
            ContentTypes = { "application/problem+json" }
        };
        return objectResult;
    }
}
