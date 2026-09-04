using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface IAgentActionRecordRepository
{
    Task<AgentActionRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentActionRecord>> GetPendingActionsAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsPendingActionAsync(string agentId, string proposedActionType, string payloadJson, CancellationToken cancellationToken = default);
    Task AddAsync(AgentActionRecord record, CancellationToken cancellationToken = default);
    void Update(AgentActionRecord record);
}
