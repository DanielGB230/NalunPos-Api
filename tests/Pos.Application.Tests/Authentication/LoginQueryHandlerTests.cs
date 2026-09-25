using Pos.Application.Authentication.Queries;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Authentication;

public class LoginQueryHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeJwtTokenGenerator _jwtTokenGenerator = new();
    private readonly LoginQueryHandler _handler;

    public LoginQueryHandlerTests()
    {
        _handler = new LoginQueryHandler(_userRepository, _passwordHasher, _jwtTokenGenerator);
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ShouldReturnTokenAndSuccessResult()
    {
        // Arrange
        string rawPassword = "SuperAdminPass2026!";
        string hashedPassword = _passwordHasher.HashPassword(rawPassword);

        var user = User.Create(
            new Email("admin@nalunpos.com"),
            new PasswordHash(hashedPassword),
            Role.SuperAdminRoleId,
            null,
            "Super",
            "Admin"
        );
        _userRepository.Users.Add(user);

        var query = new LoginQuery("admin@nalunpos.com", rawPassword);

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("NalunPosTest_token_admin@nalunpos.com", result.Value.Token);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidPassword_ShouldReturnUnauthorizedResult()
    {
        // Arrange
        var user = User.Create(
            new Email("admin@nalunpos.com"),
            new PasswordHash(_passwordHasher.HashPassword("CorrectPass")),
            Role.SuperAdminRoleId,
            null,
            "Super",
            "Admin"
        );
        _userRepository.Users.Add(user);

        var query = new LoginQuery("admin@nalunpos.com", "WrongPass");

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Auth.InvalidCredentials", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnUnauthorizedResult()
    {
        // Arrange
        var query = new LoginQuery("noexiste@nalunpos.com", "Password123!");

        // Act
        var result = await _handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Auth.InvalidCredentials", result.Error.Code);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(Users.FirstOrDefault(u => u.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Users.Any(u => u.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase) && u.Id != excludeId));
        public Task AddAsync(User user, CancellationToken cancellationToken = default) { Users.Add(user); return Task.CompletedTask; }
        public void Update(User user) { }
        public Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActive, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<User>, int)>((Users, Users.Count));
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed_{password}";
        public bool VerifyPassword(string password, string hash) => hash == $"hashed_{password}";
        public bool Verify(string password, PasswordHash passwordHash) => passwordHash.Value == $"hashed_{password}";
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly string _issuer = "NalunPosTest";
        public string GenerateToken(User user) => $"{_issuer}_token_{user.Email.Value}";
        public string GenerateToken(User user, Role role) => $"{_issuer}_token_{user.Email.Value}";
        public string GenerateToken(User user, Tenant? tenant = null) => $"{_issuer}_token_{user.Email.Value}";
    }
}
