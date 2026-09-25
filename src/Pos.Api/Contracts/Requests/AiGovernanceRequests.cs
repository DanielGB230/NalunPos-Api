namespace Pos.Api.Contracts.Requests;

public record GetPendingActionsRequest : PaginationRequest
{
    public Pos.Domain.Enums.AgentActionStatus? Status { get; init; }
}

public record ReviewAgentActionRequest(
    string Action,
    string? Comment
);

public record ProposeAgentActionRequest(
    string AgentId,
    string ProposedActionType,
    string PayloadJson,
    Pos.Domain.Enums.RiskLevel RiskLevel
);
