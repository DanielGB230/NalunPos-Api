using Pos.Application.Common.Behaviors;
using Pos.Application.Roles;
using Pos.Application.Roles.Commands;
using Pos.Application.Roles.DTOs;
using Pos.Application.Tests.Support.Fakes;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Xunit;

namespace Pos.Application.Tests.Roles;

public class CreateRoleCommandHandlerTests
{
    private readonly FakeRoleRepository _roleRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentTenantContext _currentTenantContext = new();
    private readonly CreateRoleCommandHandler _handler;

    public CreateRoleCommandHandlerTests()
    {
        _currentTenantContext.TenantId = Guid.NewGuid();
        _handler = new CreateRoleCommandHandler(_roleRepository, _unitOfWork, _currentTenantContext, TimeProvider.System);
    }

    [Fact]
    public async Task HandleAsync_WithUniqueName_ShouldCreateRoleAndReturnSuccess()
    {
        // Arrange
        var command = new CreateRoleCommand(
            "SupervisorDeInventario",
            "Rol para supervisar stock y compras",
            new List<string> { "Inventory.Read", "Inventory.Write" }
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SupervisorDeInventario", result.Value.Name);
        Assert.Single(_roleRepository.Roles);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleNameAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var existingRole = Role.Create(Guid.NewGuid(), "SupervisorDeInventario", "Rol existente", TimeProvider.System);
        _roleRepository.Roles.Add(existingRole);

        var command = new CreateRoleCommand(
            "SupervisorDeInventario",
            "Intento de duplicar rol"
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal(RoleErrorCodes.AlreadyExists, result.Error.Code);
        Assert.Single(_roleRepository.Roles);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData(" SuperAdmin ")]
    [InlineData("TENANTADMIN")]
    public async Task HandleAsync_WithReservedRoleName_ShouldFailWithReservedValidationCode(string reservedName)
    {
        // Arrange
        var command = new CreateRoleCommand(reservedName, "Intento de escalada");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(RoleErrorCodes.NameReserved, result.Error.Code);
        Assert.Empty(_roleRepository.Roles);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData(" SuperAdmin ")]
    [InlineData("TENANTADMIN")]
    public async Task ValidationPipeline_WithReservedRoleName_ShouldReturnDomainValidationErrorWithReservedCode(string reservedName)
    {
        // Arrange
        var behavior = new ValidationBehavior<CreateRoleCommand, Result<RoleDto>>(
            _handler,
            [new CreateRoleCommandValidator()]);
        var command = new CreateRoleCommand(reservedName, "Intento vía pipeline");

        // Act
        var result = await behavior.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(RoleErrorCodes.NameReserved, result.Error.Code);
        Assert.Empty(_roleRepository.Roles);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WithReservedRoleName_ShouldReturnReservedCodeBeforeCheckingRepositoryOrInvokingRoleCreate()
    {
        // Arrange: Sin tenant ni repositorio operativo; si el handler continuara tras la guarda
        // hacia ExistsByNameAsync, RequireTenantId() o Role.Create(), lanzaría excepción o devolvería Role.Invalid.
        var spyRepo = new ThrowIfCalledRoleRepository();
        var emptyTenantContext = new FakeCurrentTenantContext { TenantId = null };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateRoleCommandHandler(spyRepo, unitOfWork, emptyTenantContext, TimeProvider.System);
        var command = new CreateRoleCommand(" SuperAdmin ", "Intento directo al handler");

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(RoleErrorCodes.NameReserved, result.Error.Code);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    private sealed class ThrowIfCalledRoleRepository : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No debía invocarse GetByIdAsync.");

        public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No debía invocarse GetByNameAsync.");

        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No debía invocarse GetAllAsync.");

        public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No debía invocarse ExistsByNameAsync.");

        public Task AddAsync(Role role, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No debía invocarse AddAsync.");

        public void Update(Role role) =>
            throw new InvalidOperationException("No debía invocarse Update.");
    }
}
