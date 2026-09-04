using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Notifications.Queries;

public record GetUserNotificationsQuery(Guid UserId, bool UnreadOnly = false) : IQuery<IReadOnlyList<SystemNotificationDto>>;

public class GetUserNotificationsQueryHandler : IQueryHandler<GetUserNotificationsQuery, IReadOnlyList<SystemNotificationDto>>
{
    private readonly ISystemNotificationRepository _notificationRepository;

    public GetUserNotificationsQueryHandler(ISystemNotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
    }

    public async Task<IReadOnlyList<SystemNotificationDto>> HandleAsync(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var items = await _notificationRepository.GetByUserIdAsync(request.UserId, request.UnreadOnly, cancellationToken);
        return items.Select(SystemNotificationDto.FromEntity).ToList();
    }
}
