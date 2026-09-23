using Pos.Application.Common.Models;

namespace Pos.Application.Sales.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de ventas.
/// Encapsula los filtros específicos de dominio (sesión, cliente, rango de fechas).
/// </summary>
public record GetSalesRequest(
    Guid? SessionId = null,
    Guid? CustomerId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : PaginationRequest;
