using FluentValidation;
using Pos.Application.Common.Validation;

namespace Pos.Application.Platform.Outbox.Queries;

public sealed class GetDeadLetterMessagesQueryValidator : AbstractValidator<GetDeadLetterMessagesQuery>
{
    public GetDeadLetterMessagesQueryValidator()
    {
        RuleFor(x => x.PageNumber).ApplyPageNumberRule();
        RuleFor(x => x.PageSize).ApplyPageSizeRule();
    }
}
