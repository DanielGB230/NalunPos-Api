using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Branches.Commands;

[RequiresTenant]
[HasPermission(Permissions.Branches.Update)]
public record ActivateBranchCommand(Guid Id) : ICommand<Result<bool>>;

public class ActivateBranchCommandHandler : ICommandHandler<ActivateBranchCommand, Result<bool>>
{
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ActivateBranchCommandHandler(IBranchRepository branchRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<bool>> HandleAsync(ActivateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (branch == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("Branch.NotFound", $"No se encontró la sucursal con el ID '{request.Id}'."));
        }

        if (branch.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("Branch.AlreadyActive", $"La sucursal con ID '{request.Id}' ya se encuentra activa."));
        }

        branch.Activate(_timeProvider);
        _branchRepository.Update(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
