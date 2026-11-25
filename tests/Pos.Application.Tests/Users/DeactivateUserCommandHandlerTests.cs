using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Users.Commands;
using Pos.Domain.Authentication;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Users;

public class DeactivateUserCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserSessionRevoker _revoker = new();
    private readonly DeactivateUserCommandHandler _handler;

    public DeactivateUserCommandHandlerTests()
    {
        _handler = new DeactivateUserCommandHandler(
            _userRepository,
            _unitOfWork,
            TimeProvider.System,
            _revoker);
    }

    [Fact]
    public async Task HandleAsync_WhenRevocationThrowsAfterSave_ShouldPropagateAndKeepUserInactive()
    {
        // Arrange
        var user = CreateActiveUser();
        _userRepository.Users.Add(user);
        _revoker.ExceptionToThrow = new InvalidOperationException("Simulated revocation failure after save");

        // Act: la excepción de la revocación se propaga sin convertirse en Result (ADR 0034).
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync(new DeactivateUserCommand(user.Id), CancellationToken.None));

        // Assert
        Assert.Equal("Simulated revocation failure after save", thrown.Message);
        // El guardado ya ocurrió antes de la revocación y el usuario queda inactivo.
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.False(user.IsActive);
        Assert.Equal(1, _revoker.CallCount);
        Assert.Equal(user.Id, _revoker.LastUserId);
        Assert.Equal(RefreshTokenRevocationReasons.UserDeactivated, _revoker.LastReason);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFoundAndNotRevoke()
    {
        // Act
        var result = await _handler.HandleAsync(new DeactivateUserCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
        Assert.Equal(0, _revoker.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsAlreadyInactive_ShouldReturnConflictAndNotRevoke()
    {
        // Arrange
        var user = CreateActiveUser();
        user.Deactivate(TimeProvider.System);
        _userRepository.Users.Add(user);

        // Act
        var result = await _handler.HandleAsync(new DeactivateUserCommand(user.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("User.AlreadyInactive", result.Error.Code);
        Assert.False(user.IsActive);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
        Assert.Equal(0, _revoker.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsActive_ShouldSaveFirstAndThenRevokeAllSessions()
    {
        // Arrange
        var user = CreateActiveUser();
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
        var result = await _handler.HandleAsync(new DeactivateUserCommand(user.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Equal(1, _revoker.CallCount);
        Assert.Equal(user.Id, _revoker.LastUserId);
        Assert.Equal(RefreshTokenRevocationReasons.UserDeactivated, _revoker.LastReason);
        Assert.Equal(user.Id, userIdAtRevoke);
        Assert.Equal(RefreshTokenRevocationReasons.UserDeactivated, reasonAtRevoke);
        Assert.Equal(1, saveCountAtRevoke);
    }

    private static User CreateActiveUser() => User.Create(
        new Email($"deactivate_{Guid.NewGuid():N}@nalunpos.test"),
        new PasswordHash("hashedPassword"),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Usuario",
        "Desactivar",
        TimeProvider.System);
}