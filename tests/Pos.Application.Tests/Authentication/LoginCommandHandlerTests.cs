using Pos.Application.Authentication.Commands.Login;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Authentication;

public class LoginCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeTokenGenerator _tokenGenerator = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(_userRepository, _passwordHasher, _tokenGenerator);
    }

    [Fact]
    public async Task HandleAsync_WithValidSuperAdminCredentials_ShouldReturnSuccessResultWithToken()
    {
        // Arrange
        var user = User.Create("superadmin@pos.com", "HASH_ARGON2", UserRole.SuperAdmin, tenantId: null, firstName: "Super", lastName: "Admin");
        _userRepository.Users.Add(user);
        _passwordHasher.ValidPassword = "Password123!";

        var command = new LoginCommand("superadmin@pos.com", "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("fake-jwt-token-for-user", result.Value.Token);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal(UserRole.SuperAdmin, result.Value.Role);
        Assert.Null(result.Value.TenantId);
    }

    [Fact]
    public async Task HandleAsync_WithValidTenantUserCredentials_ShouldReturnSuccessWithTenantId()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        var user = User.Create("admin@tenant.com", "HASH_ARGON2", UserRole.TenantAdmin, tenantId: tenantId, firstName: "Juan", lastName: "Pérez");
        _userRepository.Users.Add(user);
        _passwordHasher.ValidPassword = "SecretPassword!";

        var command = new LoginCommand("admin@tenant.com", "SecretPassword!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal(UserRole.TenantAdmin, result.Value.Role);
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
        var user = User.Create("user@tenant.com", "HASH_ARGON2", UserRole.Cajero, tenantId: Guid.NewGuid());
        _userRepository.Users.Add(user);
        _passwordHasher.ValidPassword = "RightPassword!";

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
        var user = User.Create("inactive@tenant.com", "HASH_ARGON2", UserRole.Cajero, tenantId: Guid.NewGuid());
        user.Deactivate();
        _userRepository.Users.Add(user);
        _passwordHasher.ValidPassword = "Password123!";

        var command = new LoginCommand("inactive@tenant.com", "Password123!");

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Credenciales de acceso inválidas.", result.Error.Message);
    }

    // Manual Fakes / Test Doubles
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            string normalized = email.Trim().ToLowerInvariant();
            return Task.FromResult(Users.FirstOrDefault(u => u.Email.Value == normalized));
        }

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            string normalized = email.Trim().ToLowerInvariant();
            return Task.FromResult(Users.Any(u => u.Email.Value == normalized && (excludeId == null || u.Id != excludeId.Value)));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public void Update(User user)
        {
        }

        public Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActiveOnly, CancellationToken cancellationToken = default)
        {
            return Task.FromResult< (IReadOnlyList<User>, int) >((Users.AsReadOnly(), Users.Count));
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string ValidPassword { get; set; } = "Password123!";

        public string HashPassword(string password) => $"HASHED_{password}";

        public bool VerifyPassword(string password, string passwordHash) => password == ValidPassword;

        public bool Verify(string password, PasswordHash passwordHash) => password == ValidPassword;
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public string GenerateToken(User user) => "fake-jwt-token-for-user";
    }
}
