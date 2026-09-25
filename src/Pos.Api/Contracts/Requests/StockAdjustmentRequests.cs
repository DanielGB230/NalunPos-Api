namespace Pos.Api.Contracts.Requests;

public record CreateStockAdjustmentRequest(
    Guid WarehouseId,
    Pos.Domain.Enums.StockAdjustmentReason Reason,
    List<CreateStockAdjustmentItemRequest> Items,
    string? Notes = null
);

public record CreateStockAdjustmentItemRequest(
    Guid ProductId,
    decimal Quantity
);

