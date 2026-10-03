using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Customers.Queries;

public class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    public GetCustomersQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
