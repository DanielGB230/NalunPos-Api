using Pos.Domain.Entities;

namespace Pos.Application.Sales.DTOs;

public record SaleLineItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPriceAmount,
    string Currency,
    decimal SubTotalAmount
)
{
    public static SaleLineItemDto FromEntity(SaleLineItem item)
    {
        return new SaleLineItemDto(
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

public record SaleDto(
    Guid Id,
    string ReceiptNumber,
    Guid SessionId,
    Guid? CustomerId,
    decimal SubTotalAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string Currency,
    string StatusName,
    DateTime CreatedAtUtc,
    IReadOnlyList<SaleLineItemDto> LineItems
)
{
    public static SaleDto FromEntity(Sale sale)
    {
        return new SaleDto(
            sale.Id,
            sale.ReceiptNumber,
            sale.SessionId,
            sale.CustomerId,
            sale.SubTotal.Amount,
            sale.TaxAmount.Amount,
            sale.Total.Amount,
            sale.Total.Currency,
            sale.Status.ToString(),
            sale.CreatedAtUtc,
            sale.LineItems.Select(SaleLineItemDto.FromEntity).ToList()
        );
    }
}
