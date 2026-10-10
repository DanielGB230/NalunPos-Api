using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Categories.Commands;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Xunit;

namespace Pos.Application.Tests.Categories;

public class ActivateCategoryCommandHandlerTests
{
    private readonly FakeCategoryRepository _categoryRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly ActivateCategoryCommandHandler _handler;

    public ActivateCategoryCommandHandlerTests()
    {
        _handler = new ActivateCategoryCommandHandler(_categoryRepository, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryIsInactive_ShouldActivateCategoryAndReturnSuccess()
    {
        // Arrange
        var category = Category.Create("Bebidas", "Categoría de bebidas");
        category.Deactivate();
        _categoryRepository.Categories.Add(category);

        var command = new ActivateCategoryCommand(category.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.True(category.IsActive);
        Assert.Single(_categoryRepository.Categories);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryIsAlreadyActive_ShouldReturnConflictResult()
    {
        // Arrange
        var category = Category.Create("Bebidas", "Categoría de bebidas");
        _categoryRepository.Categories.Add(category);

        var command = new ActivateCategoryCommand(category.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Category.AlreadyActive", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new ActivateCategoryCommand(Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Category.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

}
