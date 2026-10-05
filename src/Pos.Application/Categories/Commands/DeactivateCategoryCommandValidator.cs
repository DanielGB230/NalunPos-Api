using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Categories.Commands;

public class DeactivateCategoryCommandValidator : AbstractValidator<DeactivateCategoryCommand>
{
    public DeactivateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
