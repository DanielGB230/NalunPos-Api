using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Sales.Queries;

public class GetSaleByIdQueryValidator : AbstractValidator<GetSaleByIdQuery>
{
    public GetSaleByIdQueryValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
