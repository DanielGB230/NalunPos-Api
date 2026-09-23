using Pos.Application.Common.Models;

namespace Pos.Application.Inventory.DTOs;

/// <summary>
/// Objeto de consulta para el Kardex de movimientos de un almacén.
/// El default de PageSize es 20 en lugar de 10 para este dominio.
/// Solo encapsula paginación (el warehouseId va en la ruta).
/// </summary>
public record GetWarehouseMovementsRequest(
    int PageNumber = 1,
    int PageSize = 20
) : PaginationRequest(PageNumber, PageSize);

