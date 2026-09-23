using Pos.Application.Common.Models;

namespace Pos.Application.Suppliers.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de proveedores.
/// Consolida el filtro de estado en bool? IsActive.
/// </summary>
public record GetSuppliersRequest(
    string? SearchTerm = null,
    bool? IsActive = null
) : PaginationRequest;
