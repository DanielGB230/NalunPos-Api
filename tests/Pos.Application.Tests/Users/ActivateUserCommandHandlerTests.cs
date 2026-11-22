using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Users.Commands;
using Pos.Domain.Authentication;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Users;

public class ActivateUserCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserSessionRevoker _revoker = new();
    private readonly ActivateUserCommandHandler _handler;

    public ActivateUserCommandHandlerTests()
    {
        _handler = new ActivateUserCommandHandler(
            _userRepository,
            _unitOfWork,
            TimeProvider.System,
            _revoker);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFoundAndNotRevoke()
    {
        // Act
        var result = await _handler.HandleAsync(new ActivateUserCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
        Assert.Equal(0, _revoker.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsAlreadyActive_ShouldReturnConflictAndNotRevoke()
    {
        // Arrange
        var user = CreateInactiveUser();
        user.Activate(TimeProvider.System);
        _userRepository.Users.Add(user);

        // Act
        var result = await _handler.HandleAsync(new ActivateUserCommand(user.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("User.AlreadyActive", result.Error.Code);
        Assert.True(user.IsActive);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
        Assert.Equal(0, _revoker.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsInactive_ShouldRevokeAllSessionsFirstAndThenSave()
    {
        // Arrange
        var user = CreateInactiveUser();
        _userRepository.Users.Add(user);

        int? saveCountAtRevoke = null;
        Guid? userIdAtRevoke = null;
        string? reasonAtRevoke = null;
        _revoker.OnRevoke = (userId, reason) =>
        {
            saveCountAtRevoke = _unitOfWork.SaveChangesCount;
            userIdAtRevoke = userId;
            reasonAtRevoke = reason;
        };

        // Act
        var result = await _handler.HandleAsync(new ActivateUserCommand(user.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(user.IsActive);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Equal(1, _revoker.CallCount);
        Assert.Equal(user.Id, _revoker.LastUserId);
        Assert.Equal(RefreshTokenRevocationReasons.UserReactivated, _revoker.LastReason);
        Assert.Equal(user.Id, userIdAtRevoke);
        Assert.Equal(RefreshTokenRevocationReasons.UserReactivated, reasonAtRevoke);
        Assert.Equal(0, saveCountAtRevoke);
    }

    [Fact]
    public async Task HandleAsync_WhenRevocationThrows_ShouldPropagateAndNotSaveNorActivate()
    {
        // Arrange
        var user = CreateInactiveUser();
        _userRepository.Users.Add(user);
        _revoker.ExceptionToThrow = new InvalidOperationException("fallo de revocación simulado");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync(new ActivateUserCommand(user.Id), CancellationToken.None));

        // Assert
        Assert.Equal("fallo de revocación simulado", exception.Message);
        Assert.False(user.IsActive);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
        Assert.Equal(1, _revoker.CallCount);
        Assert.Equal(RefreshTokenRevocationReasons.UserReactivated, _revoker.LastReason);
    }

    private static User CreateInactiveUser()
    {
        var user = User.Create(
            new Email($"activate_{Guid.NewGuid():N}@nalunpos.test"),
            new PasswordHash("hashedPassword"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Usuario",
            "Activar",
            TimeProvider.System);

        user.Deactivate(TimeProvider.System);
        return user;
    }
}
