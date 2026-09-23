using Pos.Application.Common.Models;

namespace Pos.Application.Products.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de productos.
/// Consolida los filtros de estado en un único bool? IsActive y
/// agrega el filtro opcional por CategoryId.
/// </summary>
public record GetProductsRequest(
    string? SearchTerm = null,
    Guid? CategoryId = null,
    bool? IsActive = null
) : PaginationRequest;
