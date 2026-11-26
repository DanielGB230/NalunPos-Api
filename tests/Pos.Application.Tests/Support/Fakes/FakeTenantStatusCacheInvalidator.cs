namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;

/// <summary>
/// Fake de <see cref="ITenantStatusCacheInvalidator"/> que registra las invalidaciones
/// solicitadas y permite forzar una excepción para probar el manejo de fallos.
/// </summary>
public sealed class FakeTenantStatusCacheInvalidator : ITenantStatusCacheInvalidator
{
    public List<Guid> InvalidatedTenantIds { get; } = new();

    public int InvalidationCount => InvalidatedTenantIds.Count;

    public Exception? ExceptionToThrow { get; set; }

    public Task InvalidateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        InvalidatedTenantIds.Add(tenantId);

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return Task.CompletedTask;
    }
}
