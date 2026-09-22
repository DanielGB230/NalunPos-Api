using Pos.Domain.Entities;

namespace Pos.Application.Inventory.DTOs;

/// <summary>
/// DTO de lectura para el nivel de stock actual de un producto en un almacén.
/// Expuesto por la vista "Stock Actual" del módulo de inventario.
/// </summary>
public record StockLevelDto(
    Guid Id,
    Guid ProductId,
    Guid WarehouseId,
    Guid? ContainerId,
    decimal QuantityAvailable,
    decimal QuantityReserved,
    decimal MinStockThreshold,
    decimal TotalPhysical,
    bool IsBelowMinThreshold
)
{
    public static StockLevelDto FromEntity(StockLevel s) =>
        new(
            s.Id,
            s.ProductId,
            s.WarehouseId,
            s.ContainerId,
            s.QuantityAvailable,
            s.QuantityReserved,
            s.MinStockThreshold,
            s.QuantityAvailable + s.QuantityReserved,
            s.IsBelowMinThreshold
        );
}
