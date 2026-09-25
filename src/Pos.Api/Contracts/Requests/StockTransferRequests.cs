namespace Pos.Api.Contracts.Requests;

public record GetStockTransfersRequest : PaginationRequest
{
    public Guid? SourceWarehouseId { get; init; }
    public Guid? DestinationWarehouseId { get; init; }
}

public record CreateStockTransferRequest(
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    List<CreateStockTransferItemRequest> Items,
    string? Notes = null
);

public record CreateStockTransferItemRequest(
    Guid ProductId,
    decimal Quantity
);
