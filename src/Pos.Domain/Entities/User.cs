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
public class User : AggregateRoot<Guid>
{
    public Email Email { get; private set; } = null!;
    public PasswordHash PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
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
        UserRole role,
        Guid? tenantId,
        string firstName = "",
        string lastName = "") : base(id)
    {
        ValidateTenantRoleInvariant(role, tenantId);

        Email = email ?? throw new DomainException("El correo electrónico es requerido.");
        PasswordHash = passwordHash ?? throw new DomainException("El hash de la contraseña es requerido.");
        Role = role;
        TenantId = tenantId;
        FirstName = firstName?.Trim() ?? string.Empty;
        LastName = lastName?.Trim() ?? string.Empty;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new UserCreatedDomainEvent(Id, Email.Value, Role, TenantId, CreatedAtUtc));
    }

    public static User Create(
        Email email,
        PasswordHash passwordHash,
        UserRole role,
        Guid? tenantId,
        string firstName = "",
        string lastName = "")
    {
        return new User(Guid.CreateVersion7(), email, passwordHash, role, tenantId, firstName, lastName);
    }

    public static User Create(
        string email,
        string passwordHash,
        UserRole role,
        Guid? tenantId,
        string firstName = "",
        string lastName = "")
    {
        return Create(new Email(email), new PasswordHash(passwordHash), role, tenantId, firstName, lastName);
    }

    public void UpdateDetails(string firstName, string lastName)
    {
        FirstName = firstName?.Trim() ?? string.Empty;
        LastName = lastName?.Trim() ?? string.Empty;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateEmail(Email newEmail)
    {
        Email = newEmail ?? throw new DomainException("El correo electrónico no puede ser nulo.");
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdatePassword(PasswordHash newPasswordHash)
    {
        PasswordHash = newPasswordHash ?? throw new DomainException("El hash de la contraseña no puede ser nulo.");
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangeRole(UserRole newRole, Guid? newTenantId)
    {
        ValidateTenantRoleInvariant(newRole, newTenantId);
        Role = newRole;
        TenantId = newTenantId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ValidateTenantRoleInvariant(UserRole role, Guid? tenantId)
    {
        if (role == UserRole.SuperAdmin)
        {
            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                throw new DomainException("Un usuario SuperAdmin no debe tener un TenantId asignado.");
            }
        }
        else
        {
            if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            {
                throw new DomainException($"Un usuario con rol '{role}' debe tener un TenantId obligatorio.");
            }
        }
    }
}
