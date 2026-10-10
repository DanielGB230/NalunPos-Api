namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;

public sealed class FakeProductRepository : IProductRepository
{
    public List<Product> Products { get; } = [];
    public Product? ProductToReturn { get; set; }
    public int CallCount { get; private set; }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (ProductToReturn != null) return Task.FromResult<Product?>(ProductToReturn);
        return Task.FromResult(Products.FirstOrDefault(p => p.Id == id));
    }

    public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(Products.FirstOrDefault(p => p.Sku.Value == sku.Value));
    }

    public Task<bool> ExistsBySkuAsync(Sku sku, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(Products.Any(p => p.Sku.Value == sku.Value && (excludeId == null || p.Id != excludeId.Value)));
    }

    public Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        CallCount++;
        Products.Add(product);
        return Task.CompletedTask;
    }

    public void Update(Product product)
    {
        CallCount++;
    }

    public void Delete(Product product)
    {
        CallCount++;
        Products.Remove(product);
    }

    public Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? searchTerm,
        Guid? categoryId = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult<(IReadOnlyList<Product>, int)>((Products, Products.Count));
    }
}
