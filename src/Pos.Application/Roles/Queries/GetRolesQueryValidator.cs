using FluentValidation;

namespace Pos.Application.Roles.Queries;

public class GetRolesQueryValidator : AbstractValidator<GetRolesQuery>
{
    public GetRolesQueryValidator()
    {
    }
}
