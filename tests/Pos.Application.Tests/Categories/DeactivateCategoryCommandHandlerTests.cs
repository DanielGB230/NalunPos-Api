using Pos.Application.Categories.Commands;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Xunit;

namespace Pos.Application.Tests.Categories;

public class DeactivateCategoryCommandHandlerTests
{
    private readonly FakeCategoryRepository _categoryRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly DeactivateCategoryCommandHandler _handler;

    public DeactivateCategoryCommandHandlerTests()
    {
        _handler = new DeactivateCategoryCommandHandler(_categoryRepository, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryIsActive_ShouldDeactivateCategoryAndReturnSuccess()
    {
        // Arrange
        var category = Category.Create("Bebidas", "Categoría de bebidas");
        _categoryRepository.Categories.Add(category);

        var command = new DeactivateCategoryCommand(category.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.False(category.IsActive);
        Assert.Single(_categoryRepository.Categories);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryIsAlreadyInactive_ShouldReturnConflictResult()
    {
        // Arrange
        var category = Category.Create("Bebidas", "Categoría de bebidas");
        category.Deactivate();
        _categoryRepository.Categories.Add(category);

        var command = new DeactivateCategoryCommand(category.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Category.AlreadyInactive", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new DeactivateCategoryCommand(Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Category.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = [];

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Category>>(Categories);
        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));
        public Task<Category?> GetByNameAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Categories.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Categories.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && c.Id != excludeId));
        public Task AddAsync(Category category, CancellationToken cancellationToken = default) { Categories.Add(category); return Task.CompletedTask; }
        public void Update(Category category) { }
        public void Delete(Category category) => Categories.Remove(category);
        public Task<(IReadOnlyList<Category> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActiveOnly, bool includeInactive = false, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<Category>, int)>((Categories, Categories.Count));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }
}
