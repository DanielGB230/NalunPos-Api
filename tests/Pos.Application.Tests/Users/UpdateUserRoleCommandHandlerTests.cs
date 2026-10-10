using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Users;

public class UpdateUserRoleCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateUserRoleCommandHandler _handler;

    public UpdateUserRoleCommandHandlerTests()
    {
        _handler = new UpdateUserRoleCommandHandler(_userRepository, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithValidRole_ShouldUpdateRoleAndReturnSuccess()
    {
        // Arrange
        var user = User.Create(
            new Email("usuario.role@nalunpos.com"),
            new PasswordHash("hashedPassword"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pedro",
            "Gomez"
        );
        _userRepository.Users.Add(user);

        var newRoleId = Guid.NewGuid();
        var command = new UpdateUserRoleCommand(
            user.Id,
            newRoleId,
            user.TenantId
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(newRoleId, result.Value.RoleId);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateUserRoleCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

}
