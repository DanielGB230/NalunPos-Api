using Pos.Application.Common.Interfaces;
using Pos.Application.Branches.DTOs;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Branches.Queries;

public record GetBranchByIdQuery(Guid Id) : IQuery<BranchDto>;

public class GetBranchByIdQueryHandler : IQueryHandler<GetBranchByIdQuery, BranchDto>
{
    private readonly IBranchRepository _branchRepository;

    public GetBranchByIdQueryHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
    }

    public async Task<BranchDto> HandleAsync(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BranchNotFoundException(request.Id);

        return BranchDto.FromEntity(branch);
    }
}
