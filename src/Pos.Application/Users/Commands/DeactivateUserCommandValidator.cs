using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Users.Commands;

public class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
