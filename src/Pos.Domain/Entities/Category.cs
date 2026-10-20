using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para Categoría de Productos en el Catálogo Commercial.
/// Encapsulamiento estricto: mutaciones únicamente mediante métodos explícitos de dominio.
/// </summary>
public class Category : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    // Constructor privado para EF Core
    private Category()
    {
    }

    private Category(Guid id, Guid tenantId, string name, string? description, TimeProvider? timeProvider = null) : base(id)
    {
        TenantId = TenantGuard.RequireTenantId(tenantId, "una categoría");
        SetName(name);
        Description = description?.Trim();
        IsActive = true;
        CreatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new CategoryCreatedDomainEvent(Id, Name, CreatedAtUtc));
    }

    public static Category Create(Guid tenantId, string name, string? description = null, TimeProvider? timeProvider = null)
    {
        return new Category(Guid.NewGuid(), tenantId, name, description, timeProvider);
    }

    public void Update(string name, string? description, TimeProvider? timeProvider = null)
    {
        SetName(name);
        Description = description?.Trim();
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new CategoryUpdatedDomainEvent(Id, Name, UpdatedAtUtc.Value));
    }

    public void Activate(TimeProvider? timeProvider = null)
    {
        if (IsActive) return;
        IsActive = true;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void Deactivate(TimeProvider? timeProvider = null)
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new CategoryDeactivatedDomainEvent(Id, UpdatedAtUtc.Value));
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la categoría no puede estar vacío.");
        }

        string trimmedName = name.Trim();
        if (trimmedName.Length < 2 || trimmedName.Length > 100)
        {
            throw new DomainException("El nombre de la categoría debe tener entre 2 y 100 caracteres.");
        }

        Name = trimmedName;
    }
}
