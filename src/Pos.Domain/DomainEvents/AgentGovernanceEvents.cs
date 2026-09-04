using Pos.Domain.Common;
using Pos.Domain.Enums;

namespace Pos.Domain.DomainEvents;

public record AgentActionProposedDomainEvent(
    Guid RecordId,
    string AgentId,
    string ProposedActionType,
    RiskLevel RiskLevel,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record AgentActionApprovedDomainEvent(
    Guid RecordId,
    Guid ReviewerUserId,
    DateTime OccurredOnUtc
) : IDomainEvent;

public record AgentActionRejectedDomainEvent(
    Guid RecordId,
    Guid ReviewerUserId,
    DateTime OccurredOnUtc
) : IDomainEvent;
