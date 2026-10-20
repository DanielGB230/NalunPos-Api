using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para Usuario del Sistema POS SaaS Multi-Tenant.
/// Encapsulamiento estricto. TenantId es nullable (null ÚNICAMENTE para SuperAdmin).
/// </summary>
public class User : AggregateRoot<Guid>, IOptionalTenantOwnedEntity
{
    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public Guid? TenantId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    private User()
    {
    }

    private User(
        Guid id,
        Email email,
        PasswordHash passwordHash,
        Guid roleId,
        Guid? tenantId,
        string firstName = "",
        string lastName = "",
        TimeProvider? timeProvider = null) : base(id)
    {
        Email = email ?? throw new DomainException("El correo electrónico es requerido.");
        PasswordHash = passwordHash ?? throw new DomainException("El hash de la contraseña es requerido.");
        
        if (roleId == Guid.Empty) throw new DomainException("El RoleId no puede estar vacío.");
        RoleId = roleId;
        TenantId = tenantId;
        FirstName = firstName?.Trim() ?? string.Empty;
        LastName = lastName?.Trim() ?? string.Empty;
        IsActive = true;
        var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        CreatedAtUtc = utcNow;

        RaiseDomainEvent(new UserCreatedDomainEvent(Id, Email.Value, RoleId, TenantId, CreatedAtUtc));
    }

    public static User Create(
        Email email,
        PasswordHash passwordHash,
        Guid roleId,
        Guid? tenantId,
        string firstName = "",
        string lastName = "",
        TimeProvider? timeProvider = null)
    {
        return new User(Guid.CreateVersion7(), email, passwordHash, roleId, tenantId, firstName, lastName, timeProvider);
    }

    public static User Create(
        string email,
        string passwordHash,
        Guid roleId,
        Guid? tenantId,
        string firstName = "",
        string lastName = "",
        TimeProvider? timeProvider = null)
    {
        return Create(new Email(email), new PasswordHash(passwordHash), roleId, tenantId, firstName, lastName, timeProvider);
    }

    public void UpdateDetails(string firstName, string lastName, TimeProvider? timeProvider = null)
    {
        FirstName = firstName?.Trim() ?? string.Empty;
        LastName = lastName?.Trim() ?? string.Empty;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void UpdateEmail(Email newEmail, TimeProvider? timeProvider = null)
    {
        Email = newEmail ?? throw new DomainException("El correo electrónico no puede ser nulo.");
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void UpdatePassword(PasswordHash newPasswordHash, TimeProvider? timeProvider = null)
    {
        PasswordHash = newPasswordHash ?? throw new DomainException("El hash de la contraseña no puede ser nulo.");
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void ChangeRole(Guid newRoleId, Guid? newTenantId, TimeProvider? timeProvider = null)
    {
        if (newRoleId == Guid.Empty) throw new DomainException("El RoleId no puede estar vacío.");

        if (RoleId == newRoleId && TenantId == newTenantId)
        {
            return;
        }

        var oldRoleId = RoleId;
        var oldTenantId = TenantId;

        RoleId = newRoleId;
        TenantId = newTenantId;
        var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;

        RaiseDomainEvent(new UserRoleChangedDomainEvent(
            Id,
            oldRoleId,
            newRoleId,
            oldTenantId,
            newTenantId,
            UpdatedAtUtc.Value));
    }

    public void Activate(TimeProvider? timeProvider = null)
    {
        if (IsActive) return;

        IsActive = true;
        var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new UserStatusChangedDomainEvent(Id, TenantId, IsActive, UpdatedAtUtc.Value));
    }

    public void Deactivate(TimeProvider? timeProvider = null)
    {
        if (!IsActive) return;

        IsActive = false;
        var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new UserStatusChangedDomainEvent(Id, TenantId, IsActive, UpdatedAtUtc.Value));
    }
}
