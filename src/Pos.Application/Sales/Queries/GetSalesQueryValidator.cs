using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Sales.Queries;

public class GetSalesQueryValidator : AbstractValidator<GetSalesQuery>
{
    public GetSalesQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
