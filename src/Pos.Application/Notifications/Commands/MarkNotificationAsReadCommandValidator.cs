using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Notifications.Commands;

public class MarkNotificationAsReadCommandValidator : AbstractValidator<MarkNotificationAsReadCommand>
{
    public MarkNotificationAsReadCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
