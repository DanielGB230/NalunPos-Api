using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Common.Models;
using Pos.Application.AI.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.AI.Queries;

[HasPermission(Permissions.AiGovernance.View)]
public record GetPendingAgentActionsQuery(int PageNumber = 1, int PageSize = 20) : IQuery<PagedResult<AgentActionRecordDto>>;

public class GetPendingAgentActionsQueryHandler : IQueryHandler<GetPendingAgentActionsQuery, PagedResult<AgentActionRecordDto>>
{
    private readonly IAgentActionRecordRepository _repository;

    public GetPendingAgentActionsQueryHandler(IAgentActionRecordRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PagedResult<AgentActionRecordDto>> HandleAsync(GetPendingAgentActionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.GetPendingActionsPagedAsync(request.PageNumber, request.PageSize, cancellationToken);
        var dtos = items.Select(AgentActionRecordDto.FromEntity).ToList();

        return new PagedResult<AgentActionRecordDto>(dtos, request.PageNumber, request.PageSize, totalCount);
    }
}
