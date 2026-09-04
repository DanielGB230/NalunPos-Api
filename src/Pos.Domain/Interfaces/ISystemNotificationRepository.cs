using Pos.Domain.Entities;

namespace Pos.Domain.Interfaces;

public interface ISystemNotificationRepository
{
    Task<SystemNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemNotification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken cancellationToken = default);
    Task AddAsync(SystemNotification notification, CancellationToken cancellationToken = default);
    void Update(SystemNotification notification);
}
