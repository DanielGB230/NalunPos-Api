using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Notifications.Commands;

[AuthenticatedOnly]
public record MarkNotificationAsReadCommand(Guid Id) : ICommand<Result<SystemNotificationDto>>;

[AuthenticatedOnly]
public class MarkNotificationAsReadCommandHandler : ICommandHandler<MarkNotificationAsReadCommand, Result<SystemNotificationDto>>
{
    private readonly ISystemNotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationAsReadCommandHandler(
        ISystemNotificationRepository notificationRepository,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<SystemNotificationDto>> HandleAsync(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notificationRepository.GetByIdAsync(request.Id, cancellationToken);
        if (notification == null)
        {
            return Result.Fail<SystemNotificationDto>(DomainError.NotFound("SystemNotification.NotFound", $"No se encontró la notificación con el ID '{request.Id}'."));
        }

        notification.MarkAsRead();

        _notificationRepository.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(SystemNotificationDto.FromEntity(notification));
    }
}
