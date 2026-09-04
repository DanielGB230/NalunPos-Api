namespace Pos.Application.Inventory.DTOs;

public record ProductStockDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal CurrentStockCalculated
);
