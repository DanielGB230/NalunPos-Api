using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.AI.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Commands;

[RequiresTenant]
[HasPermission(Permissions.AiGovernance.ProposeAction)]
public record ProposeAgentActionCommand(
    string AgentId,
    string ProposedActionType,
    string PayloadJson,
    RiskLevel RiskLevel
) : ICommand<Result<AgentActionRecordDto>>;



public class ProposeAgentActionCommandHandler : ICommandHandler<ProposeAgentActionCommand, Result<AgentActionRecordDto>>
{
    private readonly IAgentActionRecordRepository _repository;
    private readonly ICurrentTenantContext _tenantContext;
    private readonly IUnitOfWork _unitOfWork;

    public ProposeAgentActionCommandHandler(
        IAgentActionRecordRepository repository,
        ICurrentTenantContext tenantContext,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<AgentActionRecordDto>> HandleAsync(ProposeAgentActionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Guid tenantId = _tenantContext.RequireTenantId();

        // Idempotencia: Verificar si ya existe una propuesta idéntica en estado pendiente
        bool exists = await _repository.ExistsPendingActionAsync(
            request.AgentId,
            request.ProposedActionType,
            request.PayloadJson,
            cancellationToken);

        if (exists)
        {
            return Result.Fail<AgentActionRecordDto>(DomainError.Conflict(
                "AgentAction.AlreadyExists",
                $"Ya existe una propuesta pendiente idéntica para el agente '{request.AgentId}' y la acción '{request.ProposedActionType}'."));
        }

        AgentActionRecord record;
        try
        {
            record = AgentActionRecord.Create(
                tenantId,
                request.AgentId,
                request.ProposedActionType,
                request.PayloadJson,
                request.RiskLevel);
        }
        catch (DomainException ex)
        {
            return Result.Fail<AgentActionRecordDto>(DomainError.Validation("AgentAction.Invalid", ex.Message));
        }

        await _repository.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(AgentActionRecordDto.FromEntity(record));
    }
}
