namespace Pos.Api.Contracts.Requests;

public record GetSalesRequest : PaginationRequest
{
    public Guid? CustomerId { get; init; }
    public Pos.Domain.Enums.SaleStatus? Status { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
}

public record CreateSaleRequest(
    string ReceiptNumber,
    Guid SessionId,
    Guid? CustomerId,
    List<CreateSaleItemRequest> Items,
    decimal TaxRatePercentage = 0m,
    string Currency = "USD"
);

public record CreateSaleItemRequest(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPriceAmount
);
