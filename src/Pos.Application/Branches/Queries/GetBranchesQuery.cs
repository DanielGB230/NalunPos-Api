using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Application.Branches.DTOs;
using Pos.Domain.Interfaces;

namespace Pos.Application.Branches.Queries;

[HasPermission(Permissions.Branches.View)]
public record GetBranchesQuery(bool? IsActive = null) : IQuery<IReadOnlyList<BranchDto>>;

[HasPermission(Permissions.Branches.View)]
public class GetBranchesQueryHandler : IQueryHandler<GetBranchesQuery, IReadOnlyList<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;

    public GetBranchesQueryHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
    }

    public async Task<IReadOnlyList<BranchDto>> HandleAsync(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var items = await _branchRepository.GetAllAsync(request.IsActive, cancellationToken);
        return items.Select(BranchDto.FromEntity).ToList();
    }
}
