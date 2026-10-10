using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Users.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Users;

public class UpdateUserCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuthUserLookup _authUserLookup;
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        _authUserLookup = new FakeAuthUserLookup(_userRepository.Users);
        _handler = new UpdateUserCommandHandler(_userRepository, _unitOfWork, _authUserLookup);
    }

    [Fact]
    public async Task HandleAsync_WithValidData_ShouldUpdateUserAndReturnSuccess()
    {
        // Arrange
        var user = User.Create(
            new Email("usuario.original@nalunpos.com"),
            new PasswordHash("hashedPassword"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Pedro",
            "Gomez"
        );
        _userRepository.Users.Add(user);

        var command = new UpdateUserCommand(
            user.Id,
            "Pedro Luis",
            "Gomez Silva",
            "pedro.actualizado@nalunpos.com"
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Pedro Luis", result.Value.FirstName);
        Assert.Equal("pedro.actualizado@nalunpos.com", result.Value.Email);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateUserCommand(
            Guid.NewGuid(),
            "Inexistente",
            "Usuario",
            "noexiste@nalunpos.com"
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
