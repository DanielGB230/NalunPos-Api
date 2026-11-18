using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Platform.Tenants.Commands.ActivateTenant;

/// <summary>
/// Handler del caso de uso de activación de un Tenant por parte del SuperAdmin.
/// </summary>
public class ActivateTenantCommandHandler : ICommandHandler<ActivateTenantCommand, Result<bool>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ActivateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<bool>> HandleAsync(ActivateTenantCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _tenantRepository.GetByIdAsync(request.Id, cancellationToken);
        if (tenant is null)
        {
            return Result.Fail<bool>(DomainError.NotFound(
                "Tenant.NotFound",
                $"No se encontró el tenant con el ID '{request.Id}'."));
        }

        var transitionResult = tenant.Activate(_timeProvider);
        if (transitionResult.IsFailure)
        {
            return Result.Fail<bool>(transitionResult.Error);
        }

        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Ok(true);
    }
}
