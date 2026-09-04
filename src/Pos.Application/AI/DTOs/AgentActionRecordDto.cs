using Pos.Domain.Entities;

namespace Pos.Application.AI.DTOs;

public record AgentActionRecordDto(
    Guid Id,
    string AgentId,
    string ProposedActionType,
    string PayloadJson,
    string RiskLevelName,
    string StatusName,
    Guid? ReviewedByUserId,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc
)
{
    public static AgentActionRecordDto FromEntity(AgentActionRecord record)
    {
        return new AgentActionRecordDto(
            record.Id,
            record.AgentId,
            record.ProposedActionType,
            record.PayloadJson,
            record.RiskLevel.ToString(),
            record.Status.ToString(),
            record.ReviewedByUserId,
            record.CreatedAtUtc,
            record.ReviewedAtUtc
        );
    }
}
