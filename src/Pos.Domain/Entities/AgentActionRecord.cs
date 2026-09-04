using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para Gobernanza de Agentes IA (Secciones 7 y 20).
/// Garantiza la trazabilidad obligatoria y el mecanismo "Human-in-the-loop" para acciones autónomas sugeridas.
/// </summary>
public class AgentActionRecord : AggregateRoot<Guid>
{
    public string AgentId { get; private set; } = string.Empty;
    public string ProposedActionType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public RiskLevel RiskLevel { get; private set; }
    public AgentActionStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }

    private AgentActionRecord()
    {
    }

    private AgentActionRecord(
        Guid id,
        string agentId,
        string proposedActionType,
        string payloadJson,
        RiskLevel riskLevel) : base(id)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new DomainException("El ID del agente IA es requerido.");
        }

        if (string.IsNullOrWhiteSpace(proposedActionType))
        {
            throw new DomainException("El tipo de acción propuesta por la IA es requerido.");
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new DomainException("El payload JSON de la propuesta no puede estar vacío.");
        }

        AgentId = agentId.Trim();
        ProposedActionType = proposedActionType.Trim();
        PayloadJson = payloadJson.Trim();
        RiskLevel = riskLevel;
        Status = AgentActionStatus.PendingApproval;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new AgentActionProposedDomainEvent(
            Id, AgentId, ProposedActionType, RiskLevel, CreatedAtUtc));
    }

    public static AgentActionRecord Create(
        string agentId,
        string proposedActionType,
        string payloadJson,
        RiskLevel riskLevel)
    {
        return new AgentActionRecord(Guid.NewGuid(), agentId, proposedActionType, payloadJson, riskLevel);
    }

    public void Approve(Guid reviewerUserId)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new DomainException("El ID del usuario revisor es requerido para aprobar la acción.");
        }

        if (Status != AgentActionStatus.PendingApproval)
        {
            throw new InvalidAgentActionStateException($"No se puede aprobar una acción de IA en estado '{Status}'.");
        }

        Status = AgentActionStatus.Approved;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new AgentActionApprovedDomainEvent(Id, reviewerUserId, ReviewedAtUtc.Value));
    }

    public void Reject(Guid reviewerUserId)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new DomainException("El ID del usuario revisor es requerido para rechazar la acción.");
        }

        if (Status != AgentActionStatus.PendingApproval)
        {
            throw new InvalidAgentActionStateException($"No se puede rechazar una acción de IA en estado '{Status}'.");
        }

        Status = AgentActionStatus.Rejected;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new AgentActionRejectedDomainEvent(Id, reviewerUserId, ReviewedAtUtc.Value));
    }

    public void MarkAsExecuted()
    {
        if (Status != AgentActionStatus.Approved)
        {
            throw new InvalidAgentActionStateException("Solo las acciones de IA previamente aprobadas por un humano pueden ser ejecutadas.");
        }

        Status = AgentActionStatus.Executed;
    }
}
