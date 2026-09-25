namespace Pos.Api.Contracts.Requests;

public record GetWarehouseStockRequest : PaginationRequest
{
    public Guid? WarehouseId { get; init; }
    public string? SearchTerm { get; init; }
}

public record GetWarehouseMovementsRequest : PaginationRequest
{
    public Guid? WarehouseId { get; init; }
    public Guid? ProductId { get; init; }
    public Pos.Domain.Enums.InventoryMovementType? MovementType { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public record GetInventoryHistoryRequest : PaginationRequest
{
    public Guid? ProductId { get; init; }
    public Guid? WarehouseId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public record RecordInventoryMovementRequest(
    Guid ProductId,
    Guid? WarehouseId,
    decimal Quantity,
    Pos.Domain.Enums.InventoryMovementType MovementType,
    Guid? ReferenceId = null,
    string? Notes = null
);
