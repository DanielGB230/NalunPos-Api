using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Interfaces;
using Pos.Infrastructure.Persistence.Context;

namespace Pos.Infrastructure.Persistence.Repositories;

public class SystemNotificationRepository : ISystemNotificationRepository
{
    private readonly PosDbContext _context;

    public SystemNotificationRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<SystemNotification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SystemNotifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SystemNotification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var query = _context.SystemNotifications.AsNoTracking().Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query.OrderByDescending(n => n.CreatedAtUtc).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SystemNotification notification, CancellationToken cancellationToken = default)
    {
        await _context.SystemNotifications.AddAsync(notification, cancellationToken);
    }

    public void Update(SystemNotification notification)
    {
        _context.SystemNotifications.Update(notification);
    }
}
