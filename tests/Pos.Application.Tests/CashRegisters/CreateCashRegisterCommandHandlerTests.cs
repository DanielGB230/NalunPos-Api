using Pos.Application.CashRegisters.Commands;
using Pos.Application.Common.Interfaces;
using Pos.Application.Tests.Support.Fakes;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.CashRegisters;

public class CreateCashRegisterCommandHandlerTests
{
    private readonly FakeCashRegisterRepository _cashRegisterRepository = new();
    private readonly FakeBranchRepository _branchRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentTenantContext _tenantContext = new() { TenantId = Guid.NewGuid() };

    [Fact]
    public async Task CreateCashRegister_WithValidBranchAndTenant_ShouldCreateRegisterAndReturnSuccess()
    {
        // Arrange
        var branch = Branch.Create(
            _tenantContext.TenantId!.Value,
            "Sucursal Centro",
            Address.Create("Calle Real 123", "Huancayo", "12001", "PE"),
            "+5164223344",
            TimeProvider.System);
        _branchRepository.Branches.Add(branch);

        var command = new CreateCashRegisterCommand(branch.Id, "Caja 01", "CR-001");
        var handler = new CreateCashRegisterCommandHandler(
            _cashRegisterRepository,
            _branchRepository,
            _unitOfWork,
            _tenantContext,
            TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Caja 01", result.Value.Name);
        Assert.Equal("CR-001", result.Value.SerialNumber);
        Assert.Single(_cashRegisterRepository.Registers);
        Assert.Equal(_tenantContext.TenantId!.Value, _cashRegisterRepository.Registers[0].TenantId);
        Assert.Equal(branch.Id, _cashRegisterRepository.Registers[0].BranchId);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CreateCashRegister_WhenBranchDoesNotExist_ShouldReturnNotFoundWithoutPersisting()
    {
        // Arrange
        var nonExistentBranchId = Guid.NewGuid();
        var command = new CreateCashRegisterCommand(nonExistentBranchId, "Caja Inexistente", "CR-404");
        var handler = new CreateCashRegisterCommandHandler(
            _cashRegisterRepository,
            _branchRepository,
            _unitOfWork,
            _tenantContext,
            TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Branch.NotFound", result.Error.Code);
        Assert.Empty(_cashRegisterRepository.Registers);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task CreateCashRegister_WhenBranchBelongsToDifferentTenant_ShouldReturnNotFoundWithoutPersisting()
    {
        // Arrange: Sucursal perteneciente a otro inquilino (Tenant B)
        var otherTenantId = Guid.NewGuid();
        var branchOfOtherTenant = Branch.Create(
            otherTenantId,
            "Sucursal Tenant B",
            Address.Create("Av. Otra 456", "Lima", "15002", "PE"),
            "+511222333",
            TimeProvider.System);
        _branchRepository.Branches.Add(branchOfOtherTenant);

        // Request intentando asociar una caja de Tenant A a la sucursal de Tenant B
        var command = new CreateCashRegisterCommand(branchOfOtherTenant.Id, "Caja Cross Tenant", "CR-CROSS");
        var handler = new CreateCashRegisterCommandHandler(
            _cashRegisterRepository,
            _branchRepository,
            _unitOfWork,
            _tenantContext,
            TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert: Debe fallar como NotFound por aislamiento de tenant
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Branch.NotFound", result.Error.Code);
        Assert.Empty(_cashRegisterRepository.Registers);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }
}
