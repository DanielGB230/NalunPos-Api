using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Suppliers.Queries;

public class GetSuppliersQueryValidator : AbstractValidator<GetSuppliersQuery>
{
    public GetSuppliersQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
