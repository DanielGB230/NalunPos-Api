using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Application.PurchaseOrders.DTOs;

public record PurchaseOrderLineDto(
    Guid Id,
    Guid ProductId,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal QuantityPending,
    bool IsFullyReceived,
    decimal UnitCostAmount,
    string Currency
);

public record PurchaseOrderDto(
    Guid Id,
    Guid SupplierId,
    Guid WarehouseId,
    string OrderNumber,
    string? Notes,
    PurchaseOrderStatus Status,
    string StatusName,
    decimal TotalOrderedCostAmount,
    string Currency,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static PurchaseOrderDto FromEntity(PurchaseOrder o)
    {
        var lines = o.Lines.Select(l => new PurchaseOrderLineDto(
            l.Id, l.ProductId,
            l.QuantityOrdered, l.QuantityReceived, l.QuantityPending, l.IsFullyReceived,
            l.UnitCost.Amount, l.UnitCost.Currency
        )).ToList();

        return new PurchaseOrderDto(
            o.Id, o.SupplierId, o.WarehouseId, o.OrderNumber, o.Notes,
            o.Status, o.Status.ToString(),
            o.TotalOrderedCost.Amount, o.TotalOrderedCost.Currency,
            lines, o.CreatedAtUtc, o.UpdatedAtUtc
        );
    }
}
