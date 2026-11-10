namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Sesión acotada para operaciones de autenticación que gestiona elevaciones temporales de
/// RLS en <c>SESSION_CONTEXT</c> y garantiza la restauración al desecharse.
/// </summary>
public interface IAuthenticationSession : IAsyncDisposable
{
    Task EnableGlobalUserLookupAsync(CancellationToken cancellationToken = default);
    Task DisableGlobalUserLookupAsync(CancellationToken cancellationToken = default);
    Task SetTenantScopeAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

