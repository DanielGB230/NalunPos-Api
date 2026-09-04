using Microsoft.AspNetCore.Mvc;
using Pos.Domain.Common;

namespace Pos.Api.Extensions;

/// <summary>
/// Métodos de extensión para convertir objetos Result / Result<T> a ActionResult de ASP.NET Core MVC.
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
        return error.Type switch
        {
            ErrorType.Unauthorized => controller.Unauthorized(new ProblemDetails
            {
                Title = "No Autorizado",
                Status = StatusCodes.Status401Unauthorized,
                Detail = error.Message,
                Instance = controller.HttpContext.Request.Path
            }),

            ErrorType.Validation => controller.BadRequest(new ProblemDetails
            {
                Title = "Error de Validación",
                Status = StatusCodes.Status400BadRequest,
                Detail = error.Message,
                Instance = controller.HttpContext.Request.Path
            }),

            ErrorType.NotFound => controller.NotFound(new ProblemDetails
            {
                Title = "Recurso no Encontrado",
                Status = StatusCodes.Status404NotFound,
                Detail = error.Message,
                Instance = controller.HttpContext.Request.Path
            }),

            ErrorType.Conflict => controller.Conflict(new ProblemDetails
            {
                Title = "Conflicto de Estado",
                Status = StatusCodes.Status409Conflict,
                Detail = error.Message,
                Instance = controller.HttpContext.Request.Path
            }),

            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Error Interno del Servidor",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "Ha ocurrido un error inesperado en el servidor. Por favor, consulte los registros del sistema.",
                Instance = controller.HttpContext.Request.Path
            })
        };
    }
}
