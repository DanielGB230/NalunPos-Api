using Pos.Application.Common.Models;
using Pos.Domain.Enums;

namespace Pos.Application.PurchaseOrders.DTOs;

/// <summary>
/// Objeto de consulta para el listado paginado de órdenes de compra.
/// Encapsula paginación y filtro de estado.
/// </summary>
public record GetPurchaseOrdersRequest(
    PurchaseOrderStatus? Status = null
) : PaginationRequest(PageSize: 20);
