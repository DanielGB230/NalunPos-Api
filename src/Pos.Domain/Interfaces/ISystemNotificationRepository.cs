using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface ISystemNotificationRepository
{
    Task<SystemNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemNotification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<SystemNotification> Items, int TotalCount)> GetPagedByUserIdAsync(Guid userId, bool unreadOnly = false, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task AddAsync(SystemNotification notification, CancellationToken cancellationToken = default);
    void Update(SystemNotification notification);
}
