using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using FluentValidation;
using Pos.Application.AI.DTOs;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Commands;

public class ReviewAgentActionCommandValidator : AbstractValidator<ReviewAgentActionCommand>
{
    public ReviewAgentActionCommandValidator()
    {
        RuleFor(x => x.RecordId)
            .NotEmpty().WithMessage("El ID del registro de propuesta es requerido.");
    }
}
