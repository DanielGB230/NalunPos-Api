using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

using Pos.Domain.Common;

namespace Pos.Application.Notifications.Commands;

[HasPermission(Permissions.Notifications.Create)]
public record CreateSystemNotificationCommand(
    Guid UserId,
    string Title,
    string Message
) : ICommand<Result<SystemNotificationDto>>;

[HasPermission(Permissions.Notifications.Create)]
public class CreateSystemNotificationCommandValidator : AbstractValidator<CreateSystemNotificationCommand>
{
    public CreateSystemNotificationCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("El ID del usuario destinatario es requerido.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es requerido.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("El mensaje es requerido.");
    }
}

[HasPermission(Permissions.Notifications.Create)]
public class CreateSystemNotificationCommandHandler : ICommandHandler<CreateSystemNotificationCommand, Result<SystemNotificationDto>>
{
    private readonly ISystemNotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSystemNotificationCommandHandler(
        ISystemNotificationRepository notificationRepository,
        IUserRepository userRepository,
        IPushNotificationService pushNotificationService,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
            notification = SystemNotification.Create(request.UserId, request.Title, request.Message);
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
