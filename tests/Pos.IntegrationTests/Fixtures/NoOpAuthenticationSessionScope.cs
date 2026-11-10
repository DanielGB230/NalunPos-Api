using Pos.Infrastructure.Authentication;

namespace Pos.IntegrationTests.Fixtures;

/// <summary>
/// Implementación No-Op de <see cref="IAuthenticationSessionScope"/> exclusiva para pruebas
/// que configuran <c>UseInMemoryDatabase</c> (ej. <see cref="CustomWebApplicationFactory"/>).
/// </summary>
public sealed class NoOpAuthenticationSessionScope : IAuthenticationSessionScope
{
    public Task<IAuthenticationSession> BeginAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IAuthenticationSession>(new NoOpSession());

    private sealed class NoOpSession : IAuthenticationSession
    {
        public Task EnableGlobalUserLookupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisableGlobalUserLookupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetTenantScopeAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

