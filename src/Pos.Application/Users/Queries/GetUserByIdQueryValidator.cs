using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Users.Queries;

public class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
{
    public GetUserByIdQueryValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
