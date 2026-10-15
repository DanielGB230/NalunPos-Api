using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Exceptions;
using Xunit;

namespace Pos.Domain.Tests;

public class PosDeviceTests
{
    [Fact]
    public void CreatePosDeviceShouldInstantiateDevice()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        Guid branchId = Guid.NewGuid();
        string name = "Caja Principal 01";
        string serialNumber = "POS-DEV-889900";

        // Act
        var device = PosDevice.Create(tenantId, branchId, name, serialNumber);

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
        var device = PosDevice.Create(tenantId, Guid.NewGuid(), "Caja 02", "MAC-001");

        // Act
        device.RecordPing();

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
            PosDevice.Create(tenantId, Guid.NewGuid(), "Caja 03", ""));
    }

    [Fact]
    public void CreateDeviceWithPlatformScopeTenantIdShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            PosDevice.Create(PlatformScope.TenantId, Guid.NewGuid(), "Caja 04", "MAC-004"));

        Assert.Equal("El ID del tenant es requerido para crear un dispositivo POS.", ex.Message);
    }

    [Fact]
    public void CreateDeviceWithEmptyTenantIdShouldThrowDomainException()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<DomainException>(() =>
            PosDevice.Create(Guid.Empty, Guid.NewGuid(), "Caja 04", "MAC-004"));

        Assert.Equal("El ID del tenant es requerido para crear un dispositivo POS.", ex.Message);
    }
}
