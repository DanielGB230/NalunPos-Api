using Pos.Application.Tests.Support.Fakes;
using Pos.Application.CashRegisters.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.CashRegisters;

public class OpenCashRegisterSessionCommandHandlerTests
{
    private readonly FakeCashRegisterRepository _registerRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly OpenCashRegisterSessionCommandHandler _handler;

    public OpenCashRegisterSessionCommandHandlerTests()
    {
        _handler = new OpenCashRegisterSessionCommandHandler(_registerRepository, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithClosedRegister_ShouldOpenSessionAndReturnSuccess()
    {
        // Arrange
        var register = CashRegister.Create(Guid.NewGuid(), Guid.NewGuid(), "Caja Principal 01", "POS-001");
        _registerRepository.Registers.Add(register);

        var command = new OpenCashRegisterSessionCommand(
            register.Id,
            UserId: Guid.NewGuid(),
            InitialAmount: 150m,
            Currency: "USD",
            Notes: "Turno Mañana");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(SessionStatus.Open.ToString(), result.Value.StatusName);
        Assert.Equal(150m, result.Value.InitialAmount);
        Assert.Single(_registerRepository.Sessions);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithAlreadyOpenSessionInRegister_ShouldReturnConflictResult()
    {
        // Arrange
        var register = CashRegister.Create(Guid.NewGuid(), Guid.NewGuid(), "Caja Secundaria 02", "POS-002");
        _registerRepository.Registers.Add(register);

        var activeSession = CashRegisterSession.Open(register.Id, Guid.NewGuid(), Money.Create(100m, "USD"), "Apertura previa");
        register.SetCurrentSession(activeSession.Id);
        _registerRepository.Sessions.Add(activeSession);

        var command = new OpenCashRegisterSessionCommand(
            register.Id,
            UserId: Guid.NewGuid(),
            InitialAmount: 200m);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("CashRegisterSession.AlreadyOpen", result.Error.Code);
        Assert.Single(_registerRepository.Sessions); // No creó una segunda sesión
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Fakes de prueba

}
