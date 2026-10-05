using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Products.Queries;

public class GetProductByIdQueryValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
