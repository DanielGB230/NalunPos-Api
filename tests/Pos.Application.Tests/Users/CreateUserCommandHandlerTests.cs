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

public class CreateUserCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuthUserLookup _authUserLookup;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _authUserLookup = new FakeAuthUserLookup(_userRepository.Users);
        _handler = new CreateUserCommandHandler(_userRepository, _passwordHasher, _unitOfWork, _authUserLookup);
    }

    [Fact]
    public async Task HandleAsync_WithUniqueEmail_ShouldCreateUserAndReturnSuccess()
    {
        // Arrange
        var command = new CreateUserCommand(
            "Carlos",
            "Mendoza",
            "carlos.mendoza@nalunpos.com",
            "SecurePassword123!",
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("carlos.mendoza@nalunpos.com", result.Value.Email);
        Assert.Single(_userRepository.Users);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenEmailAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var existingUser = User.Create(
            new Email("existente@nalunpos.com"),
            new PasswordHash("hashedPassword"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Maria",
            "Perez"
        );
        _userRepository.Users.Add(existingUser);

        var command = new CreateUserCommand(
            "Juan",
            "Perez",
            "existente@nalunpos.com",
            "Password123!",
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("User.AlreadyExists", result.Error.Code);
        Assert.Single(_userRepository.Users); // No se agregó el nuevo
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

}
