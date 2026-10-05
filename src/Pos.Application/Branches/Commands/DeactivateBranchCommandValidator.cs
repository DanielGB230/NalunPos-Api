using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Branches.Commands;

public class DeactivateBranchCommandValidator : AbstractValidator<DeactivateBranchCommand>
{
    public DeactivateBranchCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
