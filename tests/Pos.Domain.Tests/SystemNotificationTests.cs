using Pos.Domain.Common;
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
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        string title = "Alerta de Stock Bajo";
        string message = "El producto Impresora POS ha superado el umbral mínimo.";

        // Act
        var notification = SystemNotification.Create(tenantId, userId, title, message);

        // Assert
        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(tenantId, notification.TenantId);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal(title, notification.Title);
        Assert.Equal(message, notification.Message);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void CreateNotification_WithPlatformScopeTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            SystemNotification.Create(PlatformScope.TenantId, Guid.NewGuid(), "Título", "Mensaje"));

        Assert.Equal("El ID del tenant es requerido para crear una notificación.", ex.Message);
    }

    [Fact]
    public void CreateNotification_WithEmptyTenantId_ShouldThrowDomainException()
    {
        // Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            SystemNotification.Create(Guid.Empty, Guid.NewGuid(), "Título", "Mensaje"));

        Assert.Equal("El ID del tenant es requerido para crear una notificación.", ex.Message);
    }

    [Fact]
    public void MarkAsReadShouldSetIsReadToTrue()
    {
        // Arrange
        var notification = SystemNotification.Create(Guid.NewGuid(), Guid.NewGuid(), "Título", "Mensaje");

        // Act
        notification.MarkAsRead();

        // Assert
        Assert.True(notification.IsRead);
    }
}
