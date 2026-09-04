using Pos.Domain.Entities;

namespace Pos.Application.Purchases.DTOs;

public record PurchaseLineItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPriceAmount,
    string Currency,
    decimal SubTotalAmount
)
{
    public static PurchaseLineItemDto FromEntity(PurchaseLineItem item)
    {
        return new PurchaseLineItemDto(
            item.Id,
            item.ProductId,
            item.ProductName,
            item.Quantity,
            item.UnitPrice.Amount,
            item.UnitPrice.Currency,
            item.SubTotal.Amount
        );
    }
}

public record PurchaseDto(
    Guid Id,
    Guid SupplierId,
    string OrderNumber,
    decimal TotalAmount,
    string Currency,
    string StatusName,
    DateTime CreatedAtUtc,
    IReadOnlyList<PurchaseLineItemDto> LineItems
)
{
    public static PurchaseDto FromEntity(Purchase purchase)
    {
        return new PurchaseDto(
            purchase.Id,
            purchase.SupplierId,
            purchase.OrderNumber,
            purchase.TotalAmount.Amount,
            purchase.TotalAmount.Currency,
            purchase.Status.ToString(),
            purchase.CreatedAtUtc,
            purchase.LineItems.Select(PurchaseLineItemDto.FromEntity).ToList()
        );
    }
}
