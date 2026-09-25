using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.Notifications.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Notifications.Queries;

[AuthenticatedOnly]
public record GetUserNotificationsQuery(Guid UserId, bool UnreadOnly = false, int PageNumber = 1, int PageSize = 20) : IQuery<PagedResult<SystemNotificationDto>>;

[AuthenticatedOnly]
public class GetUserNotificationsQueryHandler : IQueryHandler<GetUserNotificationsQuery, PagedResult<SystemNotificationDto>>
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

    public async Task<PagedResult<SystemNotificationDto>> HandleAsync(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId != request.UserId)
        {
            throw new Pos.Domain.Exceptions.ForbiddenDomainException("No tiene permisos para ver las notificaciones de otro usuario.");
        }

        int pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        int pageSize = Math.Min(request.PageSize < 1 ? 20 : request.PageSize, 100);

        var items = await _notificationRepository.GetByUserIdAsync(request.UserId, request.UnreadOnly, cancellationToken);
        int totalCount = items.Count;
        var pagedItems = items.Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(SystemNotificationDto.FromEntity).ToList();

        return new PagedResult<SystemNotificationDto>(pagedItems, pageNumber, pageSize, totalCount);
    }
}
