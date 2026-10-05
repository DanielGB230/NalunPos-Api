using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Customers.Commands;

public class ActivateCustomerCommandValidator : AbstractValidator<ActivateCustomerCommand>
{
    public ActivateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
