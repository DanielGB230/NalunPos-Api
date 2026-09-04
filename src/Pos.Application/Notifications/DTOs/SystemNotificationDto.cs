using Pos.Domain.Entities;

namespace Pos.Application.Notifications.DTOs;

public record SystemNotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAtUtc
)
{
    public static SystemNotificationDto FromEntity(SystemNotification notification)
    {
        return new SystemNotificationDto(
            notification.Id,
            notification.UserId,
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.CreatedAtUtc
        );
    }
}
