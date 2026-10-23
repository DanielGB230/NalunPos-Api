namespace Pos.Application.Platform.Outbox.Queries;

using FluentValidation;

/// <summary>
/// Validador para la consulta de mensajes dead-letter.
/// </summary>
public sealed class GetDeadLetterMessagesQueryValidator : AbstractValidator<GetDeadLetterMessagesQuery>
{
    public GetDeadLetterMessagesQueryValidator()
    {
        RuleFor(x => x.Take)
            .GreaterThan(0)
            .WithMessage("La cantidad de mensajes a consultar debe ser mayor a cero.")
            .LessThanOrEqualTo(200)
            .WithMessage("La cantidad máxima de mensajes a consultar es 200.");
    }
}
