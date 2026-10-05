using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Suppliers.Commands;

public class ActivateSupplierCommandValidator : AbstractValidator<ActivateSupplierCommand>
{
    public ActivateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
