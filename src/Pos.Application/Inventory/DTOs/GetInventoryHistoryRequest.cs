using Pos.Application.Common.Models;

namespace Pos.Application.Inventory.DTOs;

/// <summary>
/// Objeto de consulta para el historial de movimientos de inventario de un producto.
/// Encapsula paginación y filtro opcional por almacén.
/// </summary>
public record GetInventoryHistoryRequest(
    Guid? WarehouseId = null
) : PaginationRequest;
