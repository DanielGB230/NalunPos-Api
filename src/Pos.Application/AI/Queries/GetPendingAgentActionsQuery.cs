using Pos.Application.Common.Interfaces;
using Pos.Application.AI.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Queries;

public record GetPendingAgentActionsQuery : IQuery<IReadOnlyList<AgentActionRecordDto>>;

public class GetPendingAgentActionsQueryHandler : IQueryHandler<GetPendingAgentActionsQuery, IReadOnlyList<AgentActionRecordDto>>
{
    private readonly IAgentActionRecordRepository _repository;

    public GetPendingAgentActionsQueryHandler(IAgentActionRecordRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<AgentActionRecordDto>> HandleAsync(GetPendingAgentActionsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetPendingActionsAsync(cancellationToken);
        return items.Select(AgentActionRecordDto.FromEntity).ToList();
    }
}
