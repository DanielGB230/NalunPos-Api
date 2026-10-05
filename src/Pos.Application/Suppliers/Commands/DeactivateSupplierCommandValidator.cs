using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Suppliers.Commands;

public class DeactivateSupplierCommandValidator : AbstractValidator<DeactivateSupplierCommand>
{
    public DeactivateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
