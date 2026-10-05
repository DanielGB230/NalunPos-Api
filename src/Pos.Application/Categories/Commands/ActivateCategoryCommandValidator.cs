using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Categories.Commands;

public class ActivateCategoryCommandValidator : AbstractValidator<ActivateCategoryCommand>
{
    public ActivateCategoryCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
