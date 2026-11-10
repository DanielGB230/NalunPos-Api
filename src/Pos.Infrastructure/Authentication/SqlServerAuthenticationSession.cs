namespace Pos.Infrastructure.Authentication;

/// <summary>
/// Implementación concreta de <see cref="IAuthenticationSession"/> respaldada por
/// <see cref="SqlServerAuthenticationSessionScope"/>.
/// </summary>
internal sealed class SqlServerAuthenticationSession : IAuthenticationSession
{
    private readonly SqlServerAuthenticationSessionScope _scope;
    private readonly bool _wasConnectionClosed;
    private readonly Guid? _previousTenantId;
    private readonly int? _previousIsSuperAdmin;
    private bool _disposed;

    public SqlServerAuthenticationSession(
        SqlServerAuthenticationSessionScope scope,
        bool wasConnectionClosed,
        Guid? previousTenantId,
        int? previousIsSuperAdmin)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _wasConnectionClosed = wasConnectionClosed;
        _previousTenantId = previousTenantId;
        _previousIsSuperAdmin = previousIsSuperAdmin;
    }

    public Task EnableGlobalUserLookupAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _scope.SetGlobalUserLookupAsync(enabled: true, cancellationToken);
    }

    public Task DisableGlobalUserLookupAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _scope.SetGlobalUserLookupAsync(enabled: false, cancellationToken);
    }

    public Task SetTenantScopeAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _scope.SetTenantScopeAsync(tenantId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _scope.RestoreAndCloseAsync(_wasConnectionClosed, _previousTenantId, _previousIsSuperAdmin);
    }
}

