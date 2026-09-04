using Pos.Application.Common.Interfaces;

namespace Pos.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior para validar la coherencia de autorización a nivel de tenant.
/// </summary>
public class TenantAuthorizationBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly ICurrentTenantContext _currentTenantContext;
    private readonly ICommandHandler<TRequest, TResponse> _inner;

    public TenantAuthorizationBehavior(
        ICurrentTenantContext currentTenantContext,
        ICommandHandler<TRequest, TResponse> inner)
    {
        _currentTenantContext = currentTenantContext;
        _inner = inner;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        // En esta fase estructural, el behavior permite la ejecución del pipeline.
        // En desarrollo futuro, validará que las solicitudes de tenant tengan un TenantId válido resuelto.
        return await _inner.HandleAsync(request, cancellationToken);
    }
}
