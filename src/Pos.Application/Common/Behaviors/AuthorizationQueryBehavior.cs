using System.Reflection;
using Pos.Application.Common.Attributes;
using Pos.Application.Common.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior de autorización basada en permisos (Fail-Closed) para Queries.
/// </summary>
public class AuthorizationQueryBehavior<TRequest, TResponse> : IQueryHandler<TRequest, TResponse>
    where TRequest : IQuery<TResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserPermissions _currentUserPermissions;
    private readonly IQueryHandler<TRequest, TResponse> _inner;

    public AuthorizationQueryBehavior(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICurrentUserPermissions currentUserPermissions,
        IQueryHandler<TRequest, TResponse> inner)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _currentUserPermissions = currentUserPermissions ?? throw new ArgumentNullException(nameof(currentUserPermissions));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        
        bool isPublicUseCase = requestType.GetCustomAttribute<PublicUseCaseAttribute>() != null;
        if (isPublicUseCase)
        {
            return await _inner.HandleAsync(request, cancellationToken);
        }

        bool isAuthenticatedOnly = requestType.GetCustomAttribute<AuthenticatedOnlyAttribute>() != null;
        var permissionAttributes = requestType.GetCustomAttributes<HasPermissionAttribute>().ToList();

        // Fail-Closed: If not marked with PublicUseCase, AuthenticatedOnly or HasPermission, it's denied by default.
        if (!isAuthenticatedOnly && permissionAttributes.Count == 0)
        {
            throw new UnauthorizedDomainException("Acceso denegado: el handler no declara reglas de autorización (Fail-Closed).");
        }

        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedDomainException("No se ha proporcionado un token de autenticación válido.");
        }

        var user = await _userRepository.GetByIdAsync(_currentUserService.UserId.Value, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedDomainException("El usuario autenticado no existe o está inactivo.");
        }

        if (permissionAttributes.Count > 0)
        {
            var userPermissions = await _currentUserPermissions.GetPermissionsAsync(user.TenantId, user.RoleId, cancellationToken);

            foreach (var attr in permissionAttributes)
            {
                if (!userPermissions.Contains(attr.Permission))
                {
                    throw new ForbiddenDomainException($"El usuario no tiene el permiso requerido: {attr.Permission}.");
                }
            }
        }

        return await _inner.HandleAsync(request, cancellationToken);
    }
}
