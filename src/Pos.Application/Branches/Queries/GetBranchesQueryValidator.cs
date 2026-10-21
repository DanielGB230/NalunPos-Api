using FluentValidation;

namespace Pos.Application.Branches.Queries;

public class GetBranchesQueryValidator : AbstractValidator<GetBranchesQuery>
{
    public GetBranchesQueryValidator()
    {
    }
}
