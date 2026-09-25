namespace Pos.Api.Contracts.Requests;

public record GetProductsRequest : PaginationRequest
{
    public string? SearchTerm { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
}

public record CreateProductRequest(
    string Name,
    string Sku,
    decimal PriceAmount,
    string Currency,
    Guid CategoryId,
    string? Description = null,
    string? Barcode = null,
    decimal? CostAmount = null,
    int InitialStock = 0
);

public record UpdateProductRequest(
    string Name,
    string? Description,
    string? Barcode,
    Guid CategoryId
);

public record UpdateProductPriceRequest(
    decimal PriceAmount,
    string Currency
);

