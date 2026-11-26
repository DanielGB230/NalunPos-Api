using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Interfaces;

namespace Pos.Application.Platform.Tenants.Commands.SuspendTenant;

/// <summary>
/// Handler del caso de uso de suspensión de un Tenant por parte del SuperAdmin.
/// </summary>
public class SuspendTenantCommandHandler : ICommandHandler<SuspendTenantCommand, Result<bool>>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantStatusCacheInvalidator _cacheInvalidator;
    private readonly ILogger<SuspendTenantCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public SuspendTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ITenantStatusCacheInvalidator cacheInvalidator,
        ILogger<SuspendTenantCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _tenantRepository = tenantRepository ?? throw new ArgumentNullException(nameof(tenantRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<bool>> HandleAsync(SuspendTenantCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await _tenantRepository.GetByIdAsync(request.Id, cancellationToken);
        if (tenant is null)
        {
            return Result.Fail<bool>(DomainError.NotFound(
                "Tenant.NotFound",
                $"No se encontró el tenant con el ID '{request.Id}'."));
        }

        var transitionResult = tenant.Suspend(_timeProvider);
        if (transitionResult.IsFailure)
        {
            return Result.Fail<bool>(transitionResult.Error);
        }

        _tenantRepository.Update(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await _cacheInvalidator.InvalidateAsync(tenant.Id, cancellationToken);
        }
        catch (Exception exception)
        {
            // El cambio de estado ya está confirmado: no se revierte. El TTL de la caché
            // (TenantStatusOptions.TenantStatusCacheTtlSeconds) actúa como cota de seguridad.
            _logger.LogError(
                exception,
                "El tenant {TenantId} se suspendió correctamente, pero la invalidación de la caché de estado falló; el TTL acotará la propagación.",
                tenant.Id);
        }

        return Result.Ok(true);
    }
}
