using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Roles.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Xunit;

namespace Pos.Application.Tests.Roles;

public class CreateRoleCommandHandlerTests
{
    private readonly FakeRoleRepository _roleRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentTenantContext _currentTenantContext = new();
    private readonly CreateRoleCommandHandler _handler;

    public CreateRoleCommandHandlerTests()
    {
        _handler = new CreateRoleCommandHandler(_roleRepository, _unitOfWork, _currentTenantContext);
    }

    [Fact]
    public async Task HandleAsync_WithUniqueName_ShouldCreateRoleAndReturnSuccess()
    {
        // Arrange
        var command = new CreateRoleCommand(
            "SupervisorDeInventario",
            "Rol para supervisar stock y compras",
            new List<string> { "Inventory.Read", "Inventory.Write" }
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SupervisorDeInventario", result.Value.Name);
        Assert.Single(_roleRepository.Roles);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleNameAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var existingRole = Role.Create(Guid.NewGuid(), "SupervisorDeInventario", "Rol existente");
        _roleRepository.Roles.Add(existingRole);

        var command = new CreateRoleCommand(
            "SupervisorDeInventario",
            "Intento de duplicar rol"
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Role.AlreadyExists", result.Error.Code);
        Assert.Single(_roleRepository.Roles);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

}
