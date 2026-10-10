using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Notifications;

public class NotificationsCommandHandlerTests
{
    private readonly FakeNotificationRepository _notificationRepository = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakePushNotificationService _pushNotificationService = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task CreateSystemNotification_WhenUserExists_ShouldCreateNotificationAndSendPush()
    {
        // Arrange
        var user = User.Create(new Email("destinatario@test.com"), new PasswordHash("hash"), Guid.NewGuid(), Guid.NewGuid(), "Ana", "Torres");
        _userRepository.Users.Add(user);

        var command = new CreateSystemNotificationCommand(user.Id, "Alerta de Stock", "El producto X está bajo en stock.");
        var handler = new CreateSystemNotificationCommandHandler(_notificationRepository, _userRepository, _pushNotificationService, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Alerta de Stock", result.Value.Title);
        Assert.Single(_notificationRepository.Notifications);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Equal(1, _pushNotificationService.SentCount);
    }

    [Fact]
    public async Task CreateSystemNotification_WhenUserDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new CreateSystemNotificationCommand(Guid.NewGuid(), "Título", "Mensaje");
        var handler = new CreateSystemNotificationCommandHandler(_notificationRepository, _userRepository, _pushNotificationService, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Equal(0, _pushNotificationService.SentCount);
    }

    [Fact]
    public async Task MarkNotificationAsRead_WhenNotificationExists_ShouldMarkAsReadAndReturnSuccess()
    {
        // Arrange
        var notification = SystemNotification.Create(Guid.NewGuid(), "Aviso", "Mensaje de aviso");
        _notificationRepository.Notifications.Add(notification);

        var command = new MarkNotificationAsReadCommand(notification.Id);
        var handler = new MarkNotificationAsReadCommandHandler(_notificationRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsRead);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task MarkNotificationAsRead_WhenNotificationDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new MarkNotificationAsReadCommand(Guid.NewGuid());
        var handler = new MarkNotificationAsReadCommandHandler(_notificationRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("SystemNotification.NotFound", result.Error.Code);
    }

}
