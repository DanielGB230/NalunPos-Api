namespace Pos.Api.Contracts.Requests;

public record GetPurchaseOrdersRequest : PaginationRequest
{
    public Guid? SupplierId { get; init; }
    public Guid? WarehouseId { get; init; }
    public Pos.Domain.Enums.PurchaseOrderStatus? Status { get; init; }
}

public record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid WarehouseId,
    string OrderNumber,
    List<CreatePurchaseOrderItemRequest> Items,
    string? Notes = null
);

public record CreatePurchaseOrderItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCostAmount,
    string Currency = "USD"
);

public record ReceivePurchaseOrderRequest(
    List<ReceivePurchaseOrderItemRequest> Lines
);

public record ReceivePurchaseOrderItemRequest(
    Guid ProductId,
    decimal ReceivedQuantity,
    string? BatchNumber = null,
    DateTime? ExpirationDate = null
);
