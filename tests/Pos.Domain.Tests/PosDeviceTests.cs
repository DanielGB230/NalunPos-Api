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
        Guid branchId = Guid.NewGuid();
        string name = "Caja Principal 01";
        string serialNumber = "POS-DEV-889900";

        // Act
        var device = PosDevice.Create(branchId, name, serialNumber);

        // Assert
        Assert.NotEqual(Guid.Empty, device.Id);
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
        var device = PosDevice.Create(Guid.NewGuid(), "Caja 02", "MAC-001");

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
        Assert.Throws<DomainException>(() =>
            PosDevice.Create(Guid.NewGuid(), "Caja 03", ""));
    }
}
