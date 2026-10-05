using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Payments.Queries;

public class GetPaymentsBySaleIdQueryValidator : AbstractValidator<GetPaymentsBySaleIdQuery>
{
    public GetPaymentsBySaleIdQueryValidator()
    {
        RuleFor(x => x.SaleId).ApplyRequiredIdRule();
    }
}
