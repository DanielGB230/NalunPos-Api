using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Categories.Queries;

public class GetCategoryByIdQueryValidator : AbstractValidator<GetCategoryByIdQuery>
{
    public GetCategoryByIdQueryValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
