using Pos.Application.Common.Interfaces;
using Pos.Application.Products.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Products;

public class DeactivateProductCommandHandlerTests
{
    private readonly FakeProductRepository _productRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DeactivateProductCommandHandler _handler;

    public DeactivateProductCommandHandlerTests()
    {
        _handler = new DeactivateProductCommandHandler(_productRepository, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WhenProductIsActive_ShouldDeactivateProductAndReturnSuccess()
    {
        // Arrange
        var product = Product.Create("Laptop Gamer", Sku.Create("LAP-001"), Money.Create(1200m, "USD"), Guid.NewGuid());
        _productRepository.Products.Add(product);

        var command = new DeactivateProductCommand(product.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.False(product.IsActive);
        Assert.Single(_productRepository.Products);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenProductIsAlreadyInactive_ShouldReturnConflictResult()
    {
        // Arrange
        var product = Product.Create("Laptop Gamer", Sku.Create("LAP-001"), Money.Create(1200m, "USD"), Guid.NewGuid());
        product.Deactivate();
        _productRepository.Products.Add(product);

        var command = new DeactivateProductCommand(product.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Product.AlreadyInactive", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenProductDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new DeactivateProductCommand(Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Product.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = [];

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Products.FirstOrDefault(p => p.Id == id));
        public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default) => Task.FromResult(Products.FirstOrDefault(p => p.Sku.Value == sku.Value));
        public Task<bool> ExistsBySkuAsync(Sku sku, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Products.Any(p => p.Sku.Value == sku.Value && (excludeId == null || p.Id != excludeId.Value)));
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) { Products.Add(product); return Task.CompletedTask; }
        public void Update(Product product) { }
        public void Delete(Product product) => Products.Remove(product);
        public Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, Guid? categoryId, bool? isActive = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Product>, int)>((Products, Products.Count));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }
}
