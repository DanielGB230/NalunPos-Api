using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Customers.Commands;

public class DeactivateCustomerCommandValidator : AbstractValidator<DeactivateCustomerCommand>
{
    public DeactivateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
