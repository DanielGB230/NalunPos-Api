namespace Pos.Application.Platform.Outbox.Queries;

using FluentValidation;
using Pos.Application.Common.Validation;

/// <summary>
/// Validador para la consulta de mensajes dead-letter.
/// </summary>
public sealed class GetDeadLetterMessagesQueryValidator : AbstractValidator<GetDeadLetterMessagesQuery>
{
    public GetDeadLetterMessagesQueryValidator()
    {
        RuleFor(x => x.Take)
            .ApplyPageSizeRule();
    }
}
