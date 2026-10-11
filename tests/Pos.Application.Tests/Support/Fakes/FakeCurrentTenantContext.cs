namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;

public sealed class FakeCurrentTenantContext : ICurrentTenantContext
{
    public Guid? TenantId { get; set; }
    public bool IsSuperAdmin => false;
    public bool HasTenant => TenantId.HasValue;
}
