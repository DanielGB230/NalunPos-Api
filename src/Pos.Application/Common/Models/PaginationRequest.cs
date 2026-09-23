namespace Pos.Application.Common.Models;

/// <summary>
/// Objeto de paginación base reutilizable para todos los endpoints [HttpGet] de listado.
/// Elimina el "Long Parameter List" encapsulando PageNumber y PageSize.
/// </summary>
public record PaginationRequest(
    int PageNumber = 1,
    int PageSize = 10
);
