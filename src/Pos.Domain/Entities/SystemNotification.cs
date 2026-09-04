using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Entidad/Agregado para Notificaciones de Sistema dirigidas a usuarios.
/// </summary>
public class SystemNotification : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private SystemNotification()
    {
    }

    private SystemNotification(
        Guid id,
        Guid userId,
        string title,
        string message) : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El ID del usuario destinatario es requerido.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("El título de la notificación es requerido.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainException("El mensaje de la notificación es requerido.");
        }

        UserId = userId;
        Title = title.Trim();
        Message = message.Trim();
        IsRead = false;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new NotificationCreatedDomainEvent(Id, UserId, Title, CreatedAtUtc));
    }

    public static SystemNotification Create(Guid userId, string title, string message)
    {
        return new SystemNotification(Guid.NewGuid(), userId, title, message);
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
