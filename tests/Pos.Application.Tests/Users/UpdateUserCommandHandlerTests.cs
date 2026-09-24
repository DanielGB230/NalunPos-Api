using Pos.Application.Common.Interfaces;
using Pos.Application.Users.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Users;

public class UpdateUserCommandHandlerTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuthUserLookup _authUserLookup;
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        _authUserLookup = new FakeAuthUserLookup(_userRepository.Users);
        _handler = new UpdateUserCommandHandler(_userRepository, _unitOfWork, _authUserLookup);
    }

    [Fact]
    public async Task HandleAsync_WithValidData_ShouldUpdateUserAndReturnSuccess()
    {
        // Arrange
        var user = User.Create(
            new Email("usuario.original@nalunpos.com"),
            new PasswordHash("hashedPassword"),
            UserRole.Cajero,
            Guid.NewGuid(),
            "Pedro",
            "Gomez"
        );
        _userRepository.Users.Add(user);

        var command = new UpdateUserCommand(
            user.Id,
            "Pedro Luis",
            "Gomez Silva",
            "pedro.actualizado@nalunpos.com",
            UserRole.Supervisor,
            user.TenantId
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Pedro Luis", result.Value.FirstName);
        Assert.Equal("pedro.actualizado@nalunpos.com", result.Value.Email);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new UpdateUserCommand(
            Guid.NewGuid(),
            "Inexistente",
            "Usuario",
            "noexiste@nalunpos.com",
            UserRole.Cajero,
            null
        );

        // Act
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("User.NotFound", result.Error.Code);
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
