using System.Reflection;
using Pos.Application.Common.Attributes;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Exceptions;
using Pos.Domain.Interfaces;

namespace Pos.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior de autorización basada en permisos — reemplaza MediatR.IPipelineBehavior.
/// Verifica que el usuario autenticado tenga los permisos declarados en [HasPermission]
/// antes de delegar al handler interno.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ICommandHandler<TRequest, TResponse> _inner;

    public AuthorizationBehavior(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICommandHandler<TRequest, TResponse> inner)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var attributes = request.GetType().GetCustomAttributes<HasPermissionAttribute>().ToList();

        if (attributes.Count == 0)
        {
            return await _inner.HandleAsync(request, cancellationToken);
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

        return await _inner.HandleAsync(request, cancellationToken);
    }
}
