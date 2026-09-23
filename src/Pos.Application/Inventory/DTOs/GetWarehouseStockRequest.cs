using Pos.Application.Common.Models;

namespace Pos.Application.Inventory.DTOs;

/// <summary>
/// Objeto de consulta para el stock de productos de un almacén.
/// Solo encapsula paginación (el warehouseId va en la ruta).
/// </summary>
public record GetWarehouseStockRequest : PaginationRequest;
