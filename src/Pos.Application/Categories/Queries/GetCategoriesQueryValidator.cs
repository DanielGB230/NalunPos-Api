using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Categories.Queries;

public class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>
{
    public GetCategoriesQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
