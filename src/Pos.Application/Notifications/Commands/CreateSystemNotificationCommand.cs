using FluentValidation;
using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Notifications.Commands;

public record CreateSystemNotificationCommand(
    Guid UserId,
    string Title,
    string Message
) : ICommand<SystemNotificationDto>;

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

public class CreateSystemNotificationCommandHandler : ICommandHandler<CreateSystemNotificationCommand, SystemNotificationDto>
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

    public async Task<SystemNotificationDto> HandleAsync(CreateSystemNotificationCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new UserNotFoundException(request.UserId);

        var notification = SystemNotification.Create(request.UserId, request.Title, request.Message);

        await _notificationRepository.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Enviar notificación Push en tiempo real mediante la Capa Anti-Corrupción (ACL)
        await _pushNotificationService.SendToUserAsync(request.UserId, request.Title, request.Message, cancellationToken);

        return SystemNotificationDto.FromEntity(notification);
    }
}
