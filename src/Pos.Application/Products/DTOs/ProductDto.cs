using Pos.Domain.Entities;

namespace Pos.Application.Products.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    string Sku,
    string? Barcode,
    decimal PriceAmount,
    string Currency,
    decimal? CostAmount,
    int StockQuantity,
    Guid CategoryId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static ProductDto FromEntity(Product product)
    {
        return new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Sku.Value,
            product.Barcode?.Value,
            product.Price.Amount,
            product.Price.Currency,
            product.Cost?.Amount,
            product.StockQuantity,
            product.CategoryId,
            product.IsActive,
            product.CreatedAtUtc,
            product.UpdatedAtUtc
        );
    }
}
