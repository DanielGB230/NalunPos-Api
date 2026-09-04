using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Notifications.Commands;

public record MarkNotificationAsReadCommand(Guid Id) : ICommand<SystemNotificationDto>;

public class MarkNotificationAsReadCommandHandler : ICommandHandler<MarkNotificationAsReadCommand, SystemNotificationDto>
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

    public async Task<SystemNotificationDto> HandleAsync(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notificationRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new SystemNotificationNotFoundException(request.Id);

        notification.MarkAsRead();

        _notificationRepository.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return SystemNotificationDto.FromEntity(notification);
    }
}
