namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Enums;

/// <summary>
/// Fake estricto y fail-closed de <see cref="ITenantStatusReader"/> para pruebas unitarias.
/// Por defecto devuelve <c>null</c> (tenant inexistente → denegar) salvo que se configure
/// explícitamente un estado, siguiendo la convención de los demás fakes de soporte.
/// </summary>
public sealed class FakeTenantStatusReader : ITenantStatusReader
{
    private readonly Dictionary<Guid, TenantStatus> _statuses = new();

    public Dictionary<Guid, TenantStatus> Statuses => _statuses;

    public Task<TenantStatus?> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _statuses.TryGetValue(tenantId, out var status);
        return Task.FromResult<TenantStatus?>(status);
    }

    public void SetStatus(Guid tenantId, TenantStatus status)
    {
        _statuses[tenantId] = status;
    }

    public void RemoveStatus(Guid tenantId)
    {
        _statuses.Remove(tenantId);
    }
}
