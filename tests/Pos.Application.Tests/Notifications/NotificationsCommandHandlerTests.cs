using Pos.Application.Common.Interfaces;
using Pos.Application.Notifications.Commands;
using Pos.Domain.Common;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Domain.Interfaces;
using Pos.Domain.ValueObjects;
using Xunit;

namespace Pos.Application.Tests.Notifications;

public class NotificationsCommandHandlerTests
{
    private readonly FakeNotificationRepository _notificationRepository = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakePushNotificationService _pushNotificationService = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task CreateSystemNotification_WhenUserExists_ShouldCreateNotificationAndSendPush()
    {
        // Arrange
        var user = User.Create(new Email("destinatario@test.com"), new PasswordHash("hash"), UserRole.Cajero, Guid.NewGuid(), "Ana", "Torres");
        _userRepository.Users.Add(user);

        var command = new CreateSystemNotificationCommand(user.Id, "Alerta de Stock", "El producto X está bajo en stock.");
        var handler = new CreateSystemNotificationCommandHandler(_notificationRepository, _userRepository, _pushNotificationService, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Alerta de Stock", result.Value.Title);
        Assert.Single(_notificationRepository.Notifications);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
        Assert.Equal(1, _pushNotificationService.SentCount);
    }

    [Fact]
    public async Task CreateSystemNotification_WhenUserDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new CreateSystemNotificationCommand(Guid.NewGuid(), "Título", "Mensaje");
        var handler = new CreateSystemNotificationCommandHandler(_notificationRepository, _userRepository, _pushNotificationService, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("User.NotFound", result.Error.Code);
        Assert.Equal(0, _pushNotificationService.SentCount);
    }

    [Fact]
    public async Task MarkNotificationAsRead_WhenNotificationExists_ShouldMarkAsReadAndReturnSuccess()
    {
        // Arrange
        var notification = SystemNotification.Create(Guid.NewGuid(), "Aviso", "Mensaje de aviso");
        _notificationRepository.Notifications.Add(notification);

        var command = new MarkNotificationAsReadCommand(notification.Id);
        var handler = new MarkNotificationAsReadCommandHandler(_notificationRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsRead);
        Assert.Equal(1, _unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task MarkNotificationAsRead_WhenNotificationDoesNotExist_ShouldReturnNotFoundResult()
    {
        // Arrange
        var command = new MarkNotificationAsReadCommand(Guid.NewGuid());
        var handler = new MarkNotificationAsReadCommandHandler(_notificationRepository, _unitOfWork);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("SystemNotification.NotFound", result.Error.Code);
    }

    private sealed class FakeNotificationRepository : ISystemNotificationRepository
    {
        public List<SystemNotification> Notifications { get; } = [];

        public Task<SystemNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Notifications.FirstOrDefault(n => n.Id == id));
        public Task AddAsync(SystemNotification notification, CancellationToken cancellationToken = default) { Notifications.Add(notification); return Task.CompletedTask; }
        public void Update(SystemNotification notification) { }
        public Task<IReadOnlyList<SystemNotification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SystemNotification>>(Notifications.Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead)).ToList());
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(Users.FirstOrDefault(u => u.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default) => Task.FromResult(Users.Any(u => u.Email.Value.Equals(email, StringComparison.OrdinalIgnoreCase) && u.Id != excludeId));
        public Task AddAsync(User user, CancellationToken cancellationToken = default) { Users.Add(user); return Task.CompletedTask; }
        public void Update(User user) { }
        public Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? searchTerm, bool? isActiveOnly, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<User>, int)>((Users, Users.Count));
    }

    private sealed class FakePushNotificationService : IPushNotificationService
    {
        public int SentCount { get; private set; }
        public Task SendToUserAsync(Guid userId, string title, string message, CancellationToken cancellationToken = default)
        {
            SentCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCount++; return Task.FromResult(1); }
    }
}
