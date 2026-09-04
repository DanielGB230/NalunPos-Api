using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class TenantTests
{
    [Fact]
    public void CreateTenantShouldStartInPendingProvisioningAndEmitEvent()
    {
        // Arrange
        string name = "Empresa Demo S.A.C.";
        string taxId = "20123456789";

        // Act
        var tenant = Tenant.Create(name, taxId);

        // Assert
        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Equal(name, tenant.Name);
        Assert.Equal(taxId, tenant.TaxId.Value);
        Assert.Equal(TenantStatus.PendingProvisioning, tenant.Status);
        Assert.Single(tenant.DomainEvents);
        Assert.IsType<DomainEvents.TenantCreatedDomainEvent>(tenant.DomainEvents.First());
    }

    [Fact]
    public void ActivateTenantShouldChangeStatusToActive()
    {
        // Arrange
        var tenant = Tenant.Create("Supermercado Central", "20987654321");

        // Act
        tenant.Activate();

        // Assert
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.NotNull(tenant.UpdatedAtUtc);
    }

    [Fact]
    public void CreateTenantWithEmptyNameShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => Tenant.Create("", "20123456789"));
    }
}
