using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Common.Interfaces;
using Pos.Application.Platform.Tenants.Commands.CreateTenant;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Platform.Tenants;

public class CreateTenantCommandHandlerTests
{
    private readonly FakeTenantRepository _tenantRepository = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeBranchRepository _branchRepository = new();
    private readonly FakeWarehouseRepository _warehouseRepository = new();
    private readonly FakeRoleRepository _roleRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly CreateTenantCommandHandler _handler;

    public CreateTenantCommandHandlerTests()
    {
        _handler = new CreateTenantCommandHandler(
            _tenantRepository,
            _userRepository,
            _branchRepository,
            _warehouseRepository,
            _roleRepository,
            _passwordHasher,
            _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WithValidData_ShouldCreateTenantAndTenantAdminUserAndReturnGuid()
    {
        // Arrange
        var command = new CreateTenantCommand(
            "Empresa POS Demo S.A.C.",
            "20601234567",
            "admin@demopos.com",
            "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        
        Assert.Single(_tenantRepository.Tenants);
        Assert.Equal("Empresa POS Demo S.A.C.", _tenantRepository.Tenants[0].Name);
        Assert.Equal("20601234567", _tenantRepository.Tenants[0].TaxId.Value);

        Assert.Single(_userRepository.Users);
        var createdUser = _userRepository.Users[0];
        Assert.Equal("admin@demopos.com", createdUser.Email.Value);
        Assert.NotEqual(Guid.Empty, createdUser.RoleId);
        Assert.Equal(result.Value, createdUser.TenantId);
        Assert.Equal("HASHED_Password123!", createdUser.PasswordHash.Value);

        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateDocumentNumber_ShouldReturnConflictError()
    {
        // Arrange
        var existingTenant = Tenant.Create("Empresa Existente", "20601234567");
        _tenantRepository.Tenants.Add(existingTenant);

        var command = new CreateTenantCommand(
            "Nueva Empresa S.A.",
            "20601234567",
            "admin@nuevaempresa.com",
            "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Tenant.AlreadyExists", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateAdminEmail_ShouldReturnConflictError()
    {
        // Arrange
        var existingTenant = Tenant.Create("Tenant 1", "20609999999");
        var existingUser = User.Create(new Email("admin@demopos.com"), new PasswordHash("HASH"), Guid.NewGuid(), existingTenant.Id);
        _userRepository.Users.Add(existingUser);

        var command = new CreateTenantCommand(
            "Empresa POS Demo 2",
            "20601234567",
            "admin@demopos.com",
            "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("User.EmailAlreadyExists", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidDomainData_ShouldReturnValidationError()
    {
        // Arrange (nombre de tenant en blanco invalida la regla de dominio)
        var command = new CreateTenantCommand(
            "   ",
            "20601234567",
            "admin@demopos.com",
            "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Tenant.Invalid", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    // Fakes de prueba

}
