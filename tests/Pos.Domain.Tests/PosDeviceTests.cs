using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class PosDeviceTests
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public void CreatePosDeviceShouldInstantiateDevice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        string name = "Caja Principal 01";
        string serialNumber = "POS-DEV-889900";

        // Act
        var device = PosDevice.Create(tenantId, branchId, name, serialNumber, _timeProvider);

        // Assert
        Assert.NotEqual(Guid.Empty, device.Id);
        Assert.Equal(tenantId, device.TenantId);
        Assert.Equal(branchId, device.BranchId);
        Assert.Equal(name, device.Name);
        Assert.Equal("POS-DEV-889900", device.SerialNumber);
        Assert.True(device.IsActive);
        Assert.Null(device.LastPingUtc);
        Assert.Single(device.DomainEvents);
    }

    [Fact]
    public void RecordPingShouldUpdateLastPingUtc()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var device = PosDevice.Create(tenantId, Guid.NewGuid(), "Caja 02", "MAC-001", _timeProvider);

        // Act
        device.RecordPing(_timeProvider);

        // Assert
        Assert.NotNull(device.LastPingUtc);
        Assert.Equal(2, device.DomainEvents.Count);
    }

    [Fact]
    public void CreateDeviceWithEmptySerialShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        var tenantId = Guid.NewGuid();
        Assert.Throws<DomainException>(() =>
            PosDevice.Create(tenantId, Guid.NewGuid(), "Caja 03", "", _timeProvider));
    }

    [Fact]
    public void CreateDeviceWithPlatformScopeTenantIdShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            PosDevice.Create(PlatformScope.TenantId, Guid.NewGuid(), "Caja 04", "MAC-004", _timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear un dispositivo POS.", ex.Message);
    }

    [Fact]
    public void CreateDeviceWithEmptyTenantIdShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            PosDevice.Create(Guid.Empty, Guid.NewGuid(), "Caja 04", "MAC-004", _timeProvider));

        Assert.Equal("El ID del tenant es requerido para crear un dispositivo POS.", ex.Message);
    }

    [Fact]
    public void CreateAndRecordPing_WithDeterministicTimeProvider_ShouldSetExactTimestampsAndEmitEvents()
    {
        // Arrange
        var registerTime = new DateTimeOffset(2026, 10, 20, 11, 0, 0, TimeSpan.Zero);
        var pingTime = new DateTimeOffset(2026, 10, 20, 11, 15, 0, TimeSpan.Zero);
        var timeProvider = new Common.FakeTimeProvider(registerTime);

        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        // Act - Create
        var device = PosDevice.Create(tenantId, branchId, "Caja Determinista", "MAC-TIME-001", timeProvider);

        // Assert - Create
        Assert.Equal(registerTime.UtcDateTime, device.CreatedAtUtc);
        var regEvent = Assert.IsType<Pos.Domain.DomainEvents.PosDeviceRegisteredDomainEvent>(Assert.Single(device.DomainEvents));
        Assert.Equal(registerTime.UtcDateTime, regEvent.OccurredOnUtc);

        // Act - RecordPing
        timeProvider.SetUtcNow(pingTime);
        device.RecordPing(timeProvider);

        // Assert - RecordPing
        Assert.Equal(pingTime, device.LastPingUtc);
        var pingEvent = device.DomainEvents.OfType<Pos.Domain.DomainEvents.PosDevicePingedDomainEvent>().Single();
        Assert.Equal(pingTime, pingEvent.LastPingUtc);
        Assert.Equal(pingTime.UtcDateTime, pingEvent.OccurredOnUtc);
    }
}
