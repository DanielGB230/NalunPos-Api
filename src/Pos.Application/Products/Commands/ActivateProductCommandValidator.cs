using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Products.Commands;

public class ActivateProductCommandValidator : AbstractValidator<ActivateProductCommand>
{
    public ActivateProductCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
