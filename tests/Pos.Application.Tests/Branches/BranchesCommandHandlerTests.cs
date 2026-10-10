using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Branches.Commands;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Branches;

public class BranchesCommandHandlerTests
{
    private readonly FakeBranchRepository _branchRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task CreateBranch_WithUniqueName_ShouldCreateBranchAndReturnSuccess()
    {
        // Arrange
        var command = new CreateBranchCommand(
            "Sucursal Centro",
            "Av. Sol 100",
            "Cusco",
            "08000",
            "PE",
            "+5184221100"
        );
        var handler = new CreateBranchCommandHandler(_branchRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Sucursal Centro", result.Value.Name);
        Assert.Single(_branchRepository.Branches);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CreateBranch_WhenNameAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var address = Address.Create("Av. Sol 100", "Cusco", "08000", "PE");
        var existingBranch = Branch.Create("Sucursal Centro", address, "+5184221100");
        _branchRepository.Branches.Add(existingBranch);

        var command = new CreateBranchCommand(
            "Sucursal Centro",
            "Otra Calle 200",
            "Cusco",
            "08000",
            "PE"
        );
        var handler = new CreateBranchCommandHandler(_branchRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Branch.AlreadyExists", result.Error.Code);
        Assert.Single(_branchRepository.Branches);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateBranch_WhenBranchDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateBranchCommand(
            Guid.NewGuid(),
            "Sucursal Inexistente",
            "Calle 1",
            "Lima",
            "15001",
            "PE",
            "+511000000"
        );
        var handler = new UpdateBranchCommandHandler(_branchRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Branch.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

}
