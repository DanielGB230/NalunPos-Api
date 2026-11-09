using Pos.Application.Tests.Support.Fakes;
using Pos.Application.Authentication.Commands.Login;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Authentication;

public class LoginCommandHandlerTests
{
    private readonly FakeAuthUserLookup _authUserLookup = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeTokenGenerator _tokenGenerator = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(_authUserLookup, _passwordHasher, _tokenGenerator, TimeProvider.System);
    }

    [Fact]
    public async Task HandleAsync_WithValidSuperAdminCredentials_ShouldReturnSuccessResultWithTokenAndSuperAdminRole()
    {
        // Arrange
        var user = User.Create(
            new Email("superadmin@pos.com"),
            new PasswordHash(_passwordHasher.HashPassword("Password123!")),
            Role.SuperAdminRoleId,
            tenantId: null,
            firstName: "Super",
            lastName: "Admin",
            timeProvider: TimeProvider.System);
        _authUserLookup.Users.Add(user);

        var command = new LoginCommand("superadmin@pos.com", "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("fake-jwt-token-for-user", result.Value.Token);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal(Role.SuperAdminRoleId, result.Value.RoleId);
        Assert.Equal("SuperAdmin", result.Value.Role);
        Assert.Equal("superadmin@pos.com", result.Value.Email);
        Assert.Equal("Super Admin", result.Value.FullName);
        Assert.Null(result.Value.TenantId);
    }

    [Fact]
    public async Task HandleAsync_WithValidTenantUserCredentials_ShouldReturnSuccessWithTenantIdAndCanonicalRole()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Guid tenantAdminRoleId = Guid.NewGuid();
        var user = User.Create(
            new Email("admin@tenant.com"),
            new PasswordHash(_passwordHasher.HashPassword("SecretPassword!")),
            tenantAdminRoleId,
            tenantId: tenantId,
            firstName: "Juan",
            lastName: "Pérez",
            timeProvider: TimeProvider.System);
        _authUserLookup.Users.Add(user);
        _authUserLookup.SetRoleName(tenantAdminRoleId, "TenantAdmin");

        var command = new LoginCommand("admin@tenant.com", "SecretPassword!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(tenantAdminRoleId, result.Value.RoleId);
        Assert.Equal("TenantAdmin", result.Value.Role);
        Assert.Equal("admin@tenant.com", result.Value.Email);
        Assert.Equal("Juan Pérez", result.Value.FullName);
    }

    [Theory]
    [InlineData("TenantAdmin")]
    [InlineData("Supervisor")]
    [InlineData("InventoryManager")]
    [InlineData("Cajero")]
    public async Task HandleAsync_WithEachCanonicalTenantRole_ShouldReturnCorrespondingCanonicalRoleName(string canonicalRoleName)
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Guid roleId = Guid.NewGuid();
        var email = $"{canonicalRoleName.ToLowerInvariant()}@tenant.com";
        var user = User.Create(
            new Email(email),
            new PasswordHash(_passwordHasher.HashPassword("SecretPassword123!")),
            roleId,
            tenantId: tenantId,
            firstName: "Operador",
            lastName: canonicalRoleName,
            timeProvider: TimeProvider.System);

        _authUserLookup.Users.Add(user);
        _authUserLookup.SetRoleName(roleId, canonicalRoleName);

        var command = new LoginCommand(email, "SecretPassword123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(canonicalRoleName, result.Value.Role);
        Assert.Equal(roleId, result.Value.RoleId);
        Assert.Equal(tenantId, result.Value.TenantId);
    }

    [Fact]
    public async Task HandleAsync_WhenRoleIsCorruptedOrMissing_ShouldFailClosedWithUnauthorized()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        Guid missingRoleId = Guid.NewGuid();
        var user = User.Create(
            new Email("corrupt@tenant.com"),
            new PasswordHash(_passwordHasher.HashPassword("Password123!")),
            missingRoleId,
            tenantId: tenantId,
            firstName: "Test",
            lastName: "Corrupt",
            timeProvider: TimeProvider.System);

        _authUserLookup.Users.Add(user);
        _authUserLookup.SetRoleName(missingRoleId, "__CORRUPTED__");

        var command = new LoginCommand("corrupt@tenant.com", "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
    }

    [Fact]
    public async Task HandleAsync_WithNonExistentEmail_ShouldReturnUnauthorizedErrorWithoutRevealingEmailNotExists()
    {
        // Arrange
        var command = new LoginCommand("nonexistent@domain.com", "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Credenciales de acceso inválidas.", result.Error.Message);
    }

    [Fact]
    public async Task HandleAsync_WithIncorrectPassword_ShouldReturnIdenticalUnauthorizedError()
    {
        // Arrange
        var user = User.Create(
            new Email("user@tenant.com"),
            new PasswordHash(_passwordHasher.HashPassword("RightPassword!")),
            Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            firstName: "User",
            lastName: "Tenant",
            timeProvider: TimeProvider.System);
        _authUserLookup.Users.Add(user);

        var command = new LoginCommand("user@tenant.com", "WrongPassword!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Credenciales de acceso inválidas.", result.Error.Message);
    }

    [Fact]
    public async Task HandleAsync_WithInactiveUser_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var user = User.Create(
            new Email("inactive@tenant.com"),
            new PasswordHash(_passwordHasher.HashPassword("Password123!")),
            Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            firstName: "User",
            lastName: "Tenant",
            timeProvider: TimeProvider.System);
        user.Deactivate(TimeProvider.System);
        _authUserLookup.Users.Add(user);

        var command = new LoginCommand("inactive@tenant.com", "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Credenciales de acceso inválidas.", result.Error.Message);
    }
}
