using Pos.Application.Common.Models;

namespace Pos.Application.Customers.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de clientes.
/// Consolida el filtro de estado en bool? IsActive.
/// </summary>
public record GetCustomersRequest(
    string? SearchTerm = null,
    bool? IsActive = null
) : PaginationRequest;
