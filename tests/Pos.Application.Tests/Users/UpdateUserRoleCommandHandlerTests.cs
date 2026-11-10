using Pos.Application.Common.Interfaces;
using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Users.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Users;

public class UpdateUserRoleCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeRoleRepository _roleRepository = new();
    private readonly FakeCurrentTenantContext _tenantContext = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateUserRoleCommandHandler _handler;

    public UpdateUserRoleCommandHandlerTests()
    {
        _tenantContext.TenantId = Guid.NewGuid();
        _handler = new UpdateUserRoleCommandHandler(
            _userRepository,
            _roleRepository,
            _tenantContext,
            _unitOfWork,
            TimeProvider.System);
    }

    [Fact]
    public async Task HandleAsync_WithValidRoleInSameTenant_ShouldUpdateRoleAndReturnSuccess()
    {
        // Arrange
        Guid tenantId = _tenantContext.RequireTenantId();
        var user = User.Create(
            new Email("usuario.role@nalunpos.com"),
            new PasswordHash("hashedPassword"),
            Guid.NewGuid(),
            tenantId,
            "Pedro",
            "Gomez",
            TimeProvider.System);
        _userRepository.Users.Add(user);

        var newRole = Role.Create(tenantId, "SupervisorDeCaja", "Rol válido del tenant", TimeProvider.System);
        _roleRepository.Roles.Add(newRole);

        var command = new UpdateUserRoleCommand(user.Id, newRole.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(newRole.Id, result.Value.RoleId);
        Assert.Equal(tenantId, user.TenantId);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateUserRoleCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleBelongsToAnotherTenant_ShouldReturnNotFoundAndNotModifyUser()
    {
        // Arrange
        Guid currentTenantId = _tenantContext.RequireTenantId();
        Guid otherTenantId = Guid.NewGuid();
        Guid originalRoleId = Guid.NewGuid();

        var user = User.Create(
            new Email("usuario@tenant1.com"),
            new PasswordHash("hashedPassword"),
            originalRoleId,
            currentTenantId,
            "Ana",
            "Lopez",
            TimeProvider.System);
        _userRepository.Users.Add(user);

        var foreignRole = Role.Create(otherTenantId, "RolDeOtroTenant", "No debe asignarse", TimeProvider.System);
        _roleRepository.Roles.Add(foreignRole);

        var command = new UpdateUserRoleCommand(user.Id, foreignRole.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Role.NotFound", result.Error.Code);
        Assert.Equal(originalRoleId, user.RoleId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleDoesNotExist_ShouldReturnNotFoundAndNotModifyUser()
    {
        // Arrange
        Guid tenantId = _tenantContext.RequireTenantId();
        Guid originalRoleId = Guid.NewGuid();

        var user = User.Create(
            new Email("usuario@tenant1.com"),
            new PasswordHash("hashedPassword"),
            originalRoleId,
            tenantId,
            "Ana",
            "Lopez",
            TimeProvider.System);
        _userRepository.Users.Add(user);

        var command = new UpdateUserRoleCommand(user.Id, Guid.NewGuid());

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Role.NotFound", result.Error.Code);
        Assert.Equal(originalRoleId, user.RoleId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleIsSuperAdminPlatformRole_ShouldReturnNotFoundAndNotModifyUser()
    {
        // Arrange
        Guid tenantId = _tenantContext.RequireTenantId();
        Guid originalRoleId = Guid.NewGuid();

        var user = User.Create(
            new Email("usuario@tenant1.com"),
            new PasswordHash("hashedPassword"),
            originalRoleId,
            tenantId,
            "Ana",
            "Lopez",
            TimeProvider.System);
        _userRepository.Users.Add(user);

        var platformRole = Role.CreatePlatformRole(
            Role.SuperAdminRoleId,
            PlatformScope.TenantId,
            PlatformRoles.SuperAdmin,
            TimeProvider.System);
        _roleRepository.Roles.Add(platformRole);

        var command = new UpdateUserRoleCommand(user.Id, Role.SuperAdminRoleId);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Role.NotFound", result.Error.Code);
        Assert.Equal(originalRoleId, user.RoleId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserBelongsToAnotherTenant_ShouldReturnNotFoundAndNotModifyUser()
    {
        // Arrange
        Guid otherTenantId = Guid.NewGuid();
        Guid originalRoleId = Guid.NewGuid();
        Guid newRoleId = Guid.NewGuid();

        var foreignUser = User.Create(
            new Email("usuario@otrotenant.com"),
            new PasswordHash("hashedPassword"),
            originalRoleId,
            otherTenantId,
            "Luis",
            "Martinez",
            TimeProvider.System);
        _userRepository.Users.Add(foreignUser);

        var currentTenantRole = Role.Create(_tenantContext.RequireTenantId(), "RolActual", "Rol del tenant actual", TimeProvider.System);
        _roleRepository.Roles.Add(currentTenantRole);

        var command = new UpdateUserRoleCommand(foreignUser.Id, currentTenantRole.Id);

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Equal(originalRoleId, foreignUser.RoleId);
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public void UpdateUserRoleCommand_ShouldNotAcceptTenantIdParameterOrProperty()
    {
        Assert.Null(typeof(UpdateUserRoleCommand).GetProperty("TenantId"));
        var constructor = typeof(UpdateUserRoleCommand).GetConstructors().Single();
        Assert.DoesNotContain(constructor.GetParameters(), p =>
            string.Equals(p.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
    }
}
