using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Notifications.Queries;

public record GetUserNotificationsQuery(Guid UserId, bool UnreadOnly = false) : IQuery<IReadOnlyList<SystemNotificationDto>>;

public class GetUserNotificationsQueryHandler : IQueryHandler<GetUserNotificationsQuery, IReadOnlyList<SystemNotificationDto>>
{
    private readonly ISystemNotificationRepository _notificationRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetUserNotificationsQueryHandler(
        ISystemNotificationRepository notificationRepository,
        ICurrentUserService currentUserService)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<IReadOnlyList<SystemNotificationDto>> HandleAsync(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("No tiene permisos para ver las notificaciones de otro usuario.");
        }

        var items = await _notificationRepository.GetByUserIdAsync(request.UserId, request.UnreadOnly, cancellationToken);
        return items.Select(SystemNotificationDto.FromEntity).ToList();
    }
}
