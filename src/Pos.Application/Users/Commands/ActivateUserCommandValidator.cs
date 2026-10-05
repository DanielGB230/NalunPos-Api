using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Users.Commands;

public class ActivateUserCommandValidator : AbstractValidator<ActivateUserCommand>
{
    public ActivateUserCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
