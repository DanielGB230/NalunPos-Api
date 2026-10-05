using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Products.Commands;

public class DeactivateProductCommandValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
