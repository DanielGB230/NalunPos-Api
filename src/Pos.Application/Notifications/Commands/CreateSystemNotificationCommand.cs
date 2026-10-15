using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

using Pos.Domain.Common;

namespace Pos.Application.Notifications.Commands;

[RequiresTenant]
[HasPermission(Permissions.Notifications.Create)]
public record CreateSystemNotificationCommand(
    Guid UserId,
    string Title,
    string Message
) : ICommand<Result<SystemNotificationDto>>;

public class CreateSystemNotificationCommandHandler : ICommandHandler<CreateSystemNotificationCommand, Result<SystemNotificationDto>>
{
    private readonly ISystemNotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantContext _tenantContext;

    public CreateSystemNotificationCommandHandler(
        ISystemNotificationRepository notificationRepository,
        IUserRepository userRepository,
        IPushNotificationService pushNotificationService,
        IUnitOfWork unitOfWork,
        ICurrentTenantContext tenantContext)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<Result<SystemNotificationDto>> HandleAsync(CreateSystemNotificationCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            return Result.Fail<SystemNotificationDto>(DomainError.NotFound("User.NotFound", $"No se encontró el usuario con el ID '{request.UserId}'."));
        }

        SystemNotification notification;
        try
        {
            notification = SystemNotification.Create(_tenantContext.RequireTenantId(), request.UserId, request.Title, request.Message);
        }
        catch (DomainException ex)
        {
            return Result.Fail<SystemNotificationDto>(DomainError.Validation("SystemNotification.Invalid", ex.Message));
        }

        await _notificationRepository.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Enviar notificación Push en tiempo real mediante la Capa Anti-Corrupción (ACL)
        await _pushNotificationService.SendToUserAsync(request.UserId, request.Title, request.Message, cancellationToken);

        return Result.Ok(SystemNotificationDto.FromEntity(notification));
    }
}
