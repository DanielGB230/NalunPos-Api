using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Products.Queries;

public class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
