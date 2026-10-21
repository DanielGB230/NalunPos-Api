using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para Rol y Permisos (RBAC).
/// Completamente desacoplado de Microsoft.AspNetCore.Identity.
/// </summary>
public class Role : AggregateRoot<Guid>, IPlatformScopedEntity
{
    public static readonly Guid SuperAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly List<string> _permissions = [];

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public IReadOnlyCollection<string> Permissions => _permissions.AsReadOnly();

    private Role()
    {
    }

    private Role(Guid id, Guid tenantId, string name, string? description, IEnumerable<string>? permissions)
        : this(id, tenantId, name, description, permissions, null)
    {
    }

    private Role(Guid id, Guid tenantId, string name, string? description, IEnumerable<string>? permissions, TimeProvider? timeProvider) : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        Description = description?.Trim();
        if (permissions != null)
        {
            _permissions.AddRange(permissions.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }

        var utcNow = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        RaiseDomainEvent(new RoleCreatedDomainEvent(Id, Name, utcNow));
    }

    public static Role Create(Guid tenantId, string name, string? description = null, IEnumerable<string>? permissions = null, TimeProvider? timeProvider = null)
    {
        return new Role(Guid.NewGuid(), tenantId, name, description, permissions, timeProvider);
    }

    public static Role CreatePlatformRole(Guid id, Guid tenantId, string name, string? description = null, IEnumerable<string>? permissions = null, TimeProvider? timeProvider = null)
    {
        return new Role(id, tenantId, name, description, permissions, timeProvider);
    }

    public void UpdatePermissions(IEnumerable<string> permissions)
    {
        _permissions.Clear();
        if (permissions != null)
        {
            _permissions.AddRange(permissions.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }
    }

    public void AddPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission)) return;
        string trimmed = permission.Trim();
        if (!_permissions.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            _permissions.Add(trimmed);
        }
    }

    public void RemovePermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission)) return;
        _permissions.RemoveAll(p => string.Equals(p, permission.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del rol no puede estar vacío.");
        }

        Name = name.Trim();
    }
}
