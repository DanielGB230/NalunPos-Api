using Pos.Application.Common.Interfaces;
using FluentValidation;
using Pos.Application.AI.DTOs;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Commands;

public record ProposeAgentActionCommand(
    string AgentId,
    string ProposedActionType,
    string PayloadJson,
    RiskLevel RiskLevel
) : ICommand<AgentActionRecordDto>;

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

public class ProposeAgentActionCommandHandler : ICommandHandler<ProposeAgentActionCommand, AgentActionRecordDto>
{
    private readonly IAgentActionRecordRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ProposeAgentActionCommandHandler(
        IAgentActionRecordRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AgentActionRecordDto> HandleAsync(ProposeAgentActionCommand request, CancellationToken cancellationToken)
    {
        // Idempotencia: Verificar si ya existe una propuesta idéntica en estado pendiente
        bool exists = await _repository.ExistsPendingActionAsync(
            request.AgentId,
            request.ProposedActionType,
            request.PayloadJson,
            cancellationToken);

        if (exists)
        {
            throw new DomainException($"Ya existe una propuesta pendiente idéntica para el agente '{request.AgentId}' y la acción '{request.ProposedActionType}'.");
        }

        var record = AgentActionRecord.Create(
            request.AgentId,
            request.ProposedActionType,
            request.PayloadJson,
            request.RiskLevel);

        await _repository.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AgentActionRecordDto.FromEntity(record);
    }
}
