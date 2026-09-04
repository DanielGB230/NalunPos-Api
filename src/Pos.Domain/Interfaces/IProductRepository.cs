using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        Guid? categoryId,
        bool? isActiveOnly,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsBySkuAsync(Sku sku, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    void Update(Product product);
    void Delete(Product product);
}
