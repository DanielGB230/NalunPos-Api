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
