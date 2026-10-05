using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Branches.Queries;

public class GetBranchByIdQueryValidator : AbstractValidator<GetBranchByIdQuery>
{
    public GetBranchByIdQueryValidator()
    {
        RuleFor(x => x.Id).ApplyRequiredIdRule();
    }
}
