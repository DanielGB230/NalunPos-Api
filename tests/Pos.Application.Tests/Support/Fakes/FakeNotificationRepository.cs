namespace Pos.Application.Tests.Support.Fakes;

using Pos.Domain.Entities;
using Pos.Domain.Interfaces;

public sealed class FakeNotificationRepository : ISystemNotificationRepository
{
    public List<SystemNotification> Notifications { get; } = [];

    public Task<SystemNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Notifications.FirstOrDefault(n => n.Id == id));
    public Task AddAsync(SystemNotification notification, CancellationToken cancellationToken = default) { Notifications.Add(notification); return Task.CompletedTask; }
    public void Update(SystemNotification notification) { }
    public Task<IReadOnlyList<SystemNotification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SystemNotification>>(Notifications.Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead)).ToList());
    public Task<(IReadOnlyList<SystemNotification> Items, int TotalCount)> GetPagedByUserIdAsync(Guid userId, bool unreadOnly = false, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var filtered = Notifications.Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead)).ToList();
        var items = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult<(IReadOnlyList<SystemNotification>, int)>((items, filtered.Count));
    }
}
