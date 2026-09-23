using Pos.Application.Common.Models;

namespace Pos.Application.Categories.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de categorías.
/// Consolida los filtros de estado en un único bool? IsActive eliminando
/// la combinación confusa de IsActiveOnly + IncludeInactive.
/// </summary>
public record GetCategoriesRequest(
    string? SearchTerm = null,
    bool? IsActive = null
) : PaginationRequest;
