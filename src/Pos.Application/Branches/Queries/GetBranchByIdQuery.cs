using Pos.Application.Common.Interfaces;
using Pos.Application.Branches.DTOs;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Branches.Queries;

public record GetBranchByIdQuery(Guid Id) : IQuery<Result<BranchDto>>;

public class GetBranchByIdQueryHandler : IQueryHandler<GetBranchByIdQuery, Result<BranchDto>>
{
    private readonly IBranchRepository _branchRepository;

    public GetBranchByIdQueryHandler(IBranchRepository branchRepository)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
    }

    public async Task<Result<BranchDto>> HandleAsync(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (branch == null)
        {
            return Result.Fail<BranchDto>(DomainError.NotFound("Branch.NotFound", $"No se encontró la sucursal con el ID '{request.Id}'."));
        }

        return Result.Ok(BranchDto.FromEntity(branch));
    }
}
