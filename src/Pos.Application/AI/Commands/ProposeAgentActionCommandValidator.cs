using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.AI.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Commands;

public class ProposeAgentActionCommandValidator : AbstractValidator<ProposeAgentActionCommand>
{
    public ProposeAgentActionCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("El ID del agente IA es requerido.");

        RuleFor(x => x.ProposedActionType)
            .NotEmpty().WithMessage("El tipo de acción propuesta es requerido.");

        RuleFor(x => x.PayloadJson)
            .NotEmpty().WithMessage("El payload JSON es requerido.");

        RuleFor(x => x.RiskLevel)
            .IsInEnum().WithMessage("El nivel de riesgo no es válido.");
    }
}
