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
