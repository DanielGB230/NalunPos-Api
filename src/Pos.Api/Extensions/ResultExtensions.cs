using Microsoft.AspNetCore.Mvc;
using Pos.Api.Common;
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

    private static ObjectResult MapErrorToActionResult(ControllerBase controller, DomainError error)
    {
        var problemDetails = PosProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, error);
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }
}
