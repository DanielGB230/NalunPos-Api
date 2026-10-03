using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.AI.Queries;

public class GetPendingAgentActionsQueryValidator : AbstractValidator<GetPendingAgentActionsQuery>
{
    public GetPendingAgentActionsQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
