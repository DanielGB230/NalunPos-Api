using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.PosDevices.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.PosDevices;

public class PosDevicesCommandHandlerTests
{
    private readonly FakePosDeviceRepository _posDeviceRepository = new();
    private readonly FakeBranchRepository _branchRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task RegisterPosDevice_WithValidBranchAndUniqueSerial_ShouldCreateDeviceAndReturnSuccess()
    {
        // Arrange
        var branch = Branch.Create("Sucursal Norte", Address.Create("Av. Norte 100", "Lima", "15001", "PE"), "+5112345678");
        _branchRepository.Branches.Add(branch);

        var command = new RegisterPosDeviceCommand(branch.Id, "Caja Principal 01", "MAC-00-11-22-33-44");
        var handler = new RegisterPosDeviceCommandHandler(_posDeviceRepository, _branchRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Caja Principal 01", result.Value.Name);
        Assert.Single(_posDeviceRepository.Devices);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task RegisterPosDevice_WhenBranchDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new RegisterPosDeviceCommand(Guid.NewGuid(), "Caja Inexistente", "MAC-99-99-99");
        var handler = new RegisterPosDeviceCommandHandler(_posDeviceRepository, _branchRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Branch.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task RegisterPosDevice_WhenSerialNumberAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var branch = Branch.Create("Sucursal Sur", Address.Create("Av. Sur 200", "Lima", "15002", "PE"), "+5112345679");
        _branchRepository.Branches.Add(branch);

        var existingDevice = PosDevice.Create(branch.Id, "Caja Existente", "MAC-00-11-22-33-44");
        _posDeviceRepository.Devices.Add(existingDevice);

        var command = new RegisterPosDeviceCommand(branch.Id, "Nueva Caja", "MAC-00-11-22-33-44");
        var handler = new RegisterPosDeviceCommandHandler(_posDeviceRepository, _branchRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("PosDevice.AlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task PingPosDevice_WhenDeviceExists_ShouldRecordPingAndReturnSuccess()
    {
        // Arrange
        var device = PosDevice.Create(Guid.NewGuid(), "POS Movil 01", "MAC-55-66-77");
        _posDeviceRepository.Devices.Add(device);

        var command = new PingPosDeviceCommand(device.Id);
        var handler = new PingPosDeviceCommandHandler(_posDeviceRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task PingPosDevice_WhenDeviceDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new PingPosDeviceCommand(Guid.NewGuid());
        var handler = new PingPosDeviceCommandHandler(_posDeviceRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("PosDevice.NotFound", result.Error.Code);
    }

}
