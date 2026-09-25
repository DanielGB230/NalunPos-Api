using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Branches.Commands;

[HasPermission(Permissions.Branches.Update)]
public record DeactivateBranchCommand(Guid Id) : ICommand<Result<bool>>;

[HasPermission(Permissions.Branches.Update)]
public class DeactivateBranchCommandHandler : ICommandHandler<DeactivateBranchCommand, Result<bool>>
{
    private readonly IBranchRepository _branchRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateBranchCommandHandler(IBranchRepository branchRepository, IUnitOfWork unitOfWork)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Result<bool>> HandleAsync(DeactivateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _branchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (branch == null)
        {
            return Result.Fail<bool>(DomainError.NotFound("Branch.NotFound", $"No se encontró la sucursal con el ID '{request.Id}'."));
        }

        if (!branch.IsActive)
        {
            return Result.Fail<bool>(DomainError.Conflict("Branch.AlreadyInactive", $"La sucursal con ID '{request.Id}' ya se encuentra inactiva."));
        }

        branch.Deactivate();
        _branchRepository.Update(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
