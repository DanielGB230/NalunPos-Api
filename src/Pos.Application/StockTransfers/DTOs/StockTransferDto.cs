using Pos.Domain.Entities;

namespace Pos.Application.StockTransfers.DTOs;

public record StockTransferLineDto(Guid ProductId, decimal Quantity);

public record StockTransferDto(
    Guid Id,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    string? Notes,
    IReadOnlyList<StockTransferLineDto> Lines,
    DateTime CreatedAtUtc
)
{
    public static StockTransferDto FromEntity(StockTransfer t) =>
        new(t.Id, t.SourceWarehouseId, t.DestinationWarehouseId, t.Notes,
            t.Lines.Select(l => new StockTransferLineDto(l.ProductId, l.Quantity)).ToList(),
            t.CreatedAtUtc);
}
