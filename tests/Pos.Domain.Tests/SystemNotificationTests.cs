using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class SystemNotificationTests
{
    [Fact]
    public void CreateNotificationShouldInstantiateUnreadNotification()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        string title = "Alerta de Stock Bajo";
        string message = "El producto Impresora POS ha superado el umbral mínimo.";

        // Act
        var notification = SystemNotification.Create(userId, title, message);

        // Assert
        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(title, notification.Title);
        Assert.Equal(message, notification.Message);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void MarkAsReadShouldSetIsReadToTrue()
    {
        // Arrange
        var notification = SystemNotification.Create(Guid.NewGuid(), "Título", "Mensaje");

        // Act
        notification.MarkAsRead();

        // Assert
        Assert.True(notification.IsRead);
    }
}
