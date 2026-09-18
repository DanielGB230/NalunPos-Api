using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Application.StockAdjustments.DTOs;

public record StockAdjustmentLineDto(Guid ProductId, decimal Quantity);

public record StockAdjustmentDto(
    Guid Id,
    Guid WarehouseId,
    StockAdjustmentReason Reason,
    string ReasonName,
    string? Notes,
    IReadOnlyList<StockAdjustmentLineDto> Lines,
    DateTime CreatedAtUtc
)
{
    public static StockAdjustmentDto FromEntity(StockAdjustment a) =>
        new(a.Id, a.WarehouseId, a.Reason, a.Reason.ToString(), a.Notes,
            a.Lines.Select(l => new StockAdjustmentLineDto(l.ProductId, l.Quantity)).ToList(),
            a.CreatedAtUtc);
}
