using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.CashRegisters.Queries;

public class GetActiveSessionQueryValidator : AbstractValidator<GetActiveSessionQuery>
{
    public GetActiveSessionQueryValidator()
    {
        RuleFor(x => x.CashRegisterId).ApplyRequiredIdRule();
    }
}
