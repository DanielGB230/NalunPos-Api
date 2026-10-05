using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Branches.Commands;

public class ActivateBranchCommandValidator : AbstractValidator<ActivateBranchCommand>
{
    public ActivateBranchCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
