using Microsoft.AspNetCore.Mvc;
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
        var (title, statusCode) = error.Type switch
        {
            ErrorType.Unauthorized  => ("No Autorizado",           StatusCodes.Status401Unauthorized),
            ErrorType.Validation    => ("Error de Validación",     StatusCodes.Status400BadRequest),
            ErrorType.NotFound      => ("Recurso no Encontrado",   StatusCodes.Status404NotFound),
            ErrorType.Conflict      => ("Conflicto de Estado",     StatusCodes.Status409Conflict),
            _                       => ("Error Interno del Servidor", StatusCodes.Status500InternalServerError)
        };

        var problemDetails = new ProblemDetails
        {
            Title    = title,
            Status   = statusCode,
            Detail   = error.Type is ErrorType.Failure
                ? "Ha ocurrido un error inesperado en el servidor. Por favor, consulte los registros del sistema."
                : error.Message,
            Instance = controller.HttpContext.Request.Path
        };

        if (error.Errors is not null)
        {
            problemDetails.Extensions["errors"] = error.Errors.Values;
        }

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }
}
