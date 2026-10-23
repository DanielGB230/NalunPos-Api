namespace Pos.Application.Platform.Outbox.Commands;

using FluentValidation;

/// <summary>
/// Validador para el comando de reactivación de mensajes dead-letter.
/// </summary>
public sealed class ReprocessDeadLetterMessagesCommandValidator : AbstractValidator<ReprocessDeadLetterMessagesCommand>
{
    public ReprocessDeadLetterMessagesCommandValidator()
    {
        RuleFor(x => x.MessageIds)
            .NotNull()
            .WithMessage("La lista de identificadores no puede ser nula.")
            .Must(ids => ids == null || ids.All(id => id != Guid.Empty))
            .WithMessage("Los identificadores de mensajes deben ser GUIDs válidos no vacíos.");
    }
}
