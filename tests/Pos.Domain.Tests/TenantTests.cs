using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Domain.Tests;

public class TenantTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void CreateTenantShouldStartInPendingProvisioningAndEmitEvent()
    {
        // Arrange
        string name = "Empresa Demo S.A.C.";
        string taxId = "20123456789";

        // Act
        var tenant = Tenant.Create(name, taxId, _timeProvider);

        // Assert
        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Equal(name, tenant.Name);
        Assert.Equal(taxId, tenant.TaxId.Value);
        Assert.Equal(TenantStatus.PendingProvisioning, tenant.Status);
        Assert.Single(tenant.DomainEvents);
        Assert.IsType<DomainEvents.TenantCreatedDomainEvent>(tenant.DomainEvents.First());
    }

    [Fact]
    public void Activate_WhenNotActive_ShouldChangeStatusAndEmitTenantStatusChangedDomainEvent()
    {
        // Arrange
        var tenant = Tenant.Create("Supermercado Central", "20987654321", _timeProvider);
        tenant.ClearDomainEvents();
        var oldStatus = tenant.Status; // PendingProvisioning

        // Act
        tenant.Activate(_timeProvider);

        // Assert
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.NotNull(tenant.UpdatedAtUtc);
        
        var statusEvent = tenant.DomainEvents.OfType<DomainEvents.TenantStatusChangedDomainEvent>().SingleOrDefault();
        Assert.NotNull(statusEvent);
        Assert.Equal(tenant.Id, statusEvent.TenantId);
        Assert.Equal(oldStatus, statusEvent.OldStatus);
        Assert.Equal(TenantStatus.Active, statusEvent.NewStatus);
        Assert.True(statusEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldNotEmitEvent()
    {
        // Arrange
        var tenant = Tenant.Create("Supermercado Central", "20987654321", _timeProvider);
        tenant.Activate(_timeProvider);
        tenant.ClearDomainEvents();

        // Act
        tenant.Activate(_timeProvider);

        // Assert
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Empty(tenant.DomainEvents.OfType<DomainEvents.TenantStatusChangedDomainEvent>());
    }

    [Fact]
    public void Suspend_WhenNotSuspended_ShouldChangeStatusAndEmitTenantStatusChangedDomainEvent()
    {
        // Arrange
        var tenant = Tenant.Create("Supermercado Central", "20987654321", _timeProvider);
        tenant.Activate(_timeProvider);
        tenant.ClearDomainEvents();
        var oldStatus = tenant.Status; // Active

        // Act
        tenant.Suspend(_timeProvider);

        // Assert
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.NotNull(tenant.UpdatedAtUtc);

        var statusEvent = tenant.DomainEvents.OfType<DomainEvents.TenantStatusChangedDomainEvent>().SingleOrDefault();
        Assert.NotNull(statusEvent);
        Assert.Equal(tenant.Id, statusEvent.TenantId);
        Assert.Equal(oldStatus, statusEvent.OldStatus);
        Assert.Equal(TenantStatus.Suspended, statusEvent.NewStatus);
        Assert.True(statusEvent.OccurredOnUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Suspend_WhenAlreadySuspended_ShouldNotEmitEvent()
    {
        // Arrange
        var tenant = Tenant.Create("Supermercado Central", "20987654321", _timeProvider);
        tenant.Suspend(_timeProvider);
        tenant.ClearDomainEvents();

        // Act
        tenant.Suspend(_timeProvider);

        // Assert
        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.Empty(tenant.DomainEvents.OfType<DomainEvents.TenantStatusChangedDomainEvent>());
    }

    [Fact]
    public void CreateTenantWithEmptyNameShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => Tenant.Create("", "20123456789", _timeProvider));
    }

    [Fact]
    public void CreateAndActivateTenant_WithDeterministicTimeProvider_ShouldSetExactTimestampsAndEmitEvents()
    {
        // Arrange
        var createdTime = new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);
        var activatedTime = new DateTimeOffset(2026, 10, 20, 15, 30, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(createdTime);

        // Act - Create
        var tenant = Tenant.Create("Retail Global SAC", "20112233445", timeProvider);

        // Assert - Create
        Assert.Equal(createdTime.UtcDateTime, tenant.CreatedAtUtc);
        var createdEvent = Assert.IsType<DomainEvents.TenantCreatedDomainEvent>(Assert.Single(tenant.DomainEvents));
        Assert.Equal(createdTime.UtcDateTime, createdEvent.OccurredOnUtc);

        // Act - Activate
        timeProvider.SetUtcNow(activatedTime);
        tenant.Activate(timeProvider);

        // Assert - Activate
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(activatedTime.UtcDateTime, tenant.UpdatedAtUtc);
        var statusEvent = tenant.DomainEvents.OfType<DomainEvents.TenantStatusChangedDomainEvent>().Single();
        Assert.Equal(activatedTime.UtcDateTime, statusEvent.OccurredOnUtc);
    }
}
