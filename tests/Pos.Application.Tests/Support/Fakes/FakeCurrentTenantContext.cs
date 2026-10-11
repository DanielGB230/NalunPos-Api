namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;

public sealed class FakeCurrentTenantContext : ICurrentTenantContext
{
    /// <summary>Identificador del tenant actual configurable para pruebas.</summary>
    public Guid? TenantId { get; set; }
    public bool IsSuperAdmin => false;
    public bool HasTenant => TenantId.HasValue;
}
