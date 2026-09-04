using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para Categoría de Productos en el Catálogo Commercial.
/// Encapsulamiento estricto: mutaciones únicamente mediante métodos explícitos de dominio.
/// </summary>
public class Category : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    // Constructor privado para EF Core
    private Category()
    {
    }

    private Category(Guid id, string name, string? description) : base(id)
    {
        SetName(name);
        Description = description?.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new CategoryCreatedDomainEvent(Id, Name, CreatedAtUtc));
    }

    public static Category Create(string name, string? description = null)
    {
        return new Category(Guid.NewGuid(), name, description);
    }

    public static Category Create(Guid id, string name, string? description = null)
    {
        return new Category(id, name, description);
    }

    public void Update(string name, string? description)
    {
        SetName(name);
        Description = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new CategoryUpdatedDomainEvent(Id, Name, UpdatedAtUtc.Value));
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;

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
