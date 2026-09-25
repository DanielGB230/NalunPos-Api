using Pos.Application.Common.Interfaces;
using Pos.Application.Users.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Users;

public class CreateUserCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuthUserLookup _authUserLookup;
    private readonly CreateUserCommandHandler _handler;

    public CreateUserCommandHandlerTests()
    {
        _authUserLookup = new FakeAuthUserLookup(_userRepository.Users);
        _handler = new CreateUserCommandHandler(_userRepository, _passwordHasher, _unitOfWork, _authUserLookup);
    }

    [Fact]
    public async Task HandleAsync_WithUniqueEmail_ShouldCreateUserAndReturnSuccess()
    {
        // Arrange
        var command = new CreateUserCommand(
            "Carlos",
            "Mendoza",
            "carlos.mendoza@nalunpos.com",
            "SecurePassword123!",
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("carlos.mendoza@nalunpos.com", result.Value.Email);
        Assert.Single(_userRepository.Users);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenEmailAlreadyExists_ShouldReturnConflictResult()
    {
        // Arrange
        var existingUser = User.Create(
            new Email("existente@nalunpos.com"),
            new PasswordHash("hashedPassword"),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Maria",
            "Perez"
        );
        _userRepository.Users.Add(existingUser);

        var command = new CreateUserCommand(
            "Juan",
            "Perez",
            "existente@nalunpos.com",
            "Password123!",
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("User.AlreadyExists", result.Error.Code);
        Assert.Single(_userRepository.Users); // No se agregó el nuevo
        Assert.Equal(0, _unitOfWork.SaveChangesCount);
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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }

    private sealed class FakeAuthUserLookup : IAuthUserLookup
    {
        private readonly List<User> _users;

        public FakeAuthUserLookup(List<User> users)
        {
            _users = users;
        }

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_users.FirstOrDefault(u => string.Equals(u.Email.Value, email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_users.Any(u => string.Equals(u.Email.Value, email, StringComparison.OrdinalIgnoreCase) && u.Id != excludeId));
        }
    }
}
