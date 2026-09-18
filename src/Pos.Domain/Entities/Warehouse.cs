using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para el Almacén físico o lógico de inventario.
///
/// Un Warehouse pertenece a un Branch (sucursal) y puede contener Containers (ubicaciones internas).
/// Cada tenant tiene al menos un Warehouse IsDefault = true, creado automáticamente
/// al aprovisionar el tenant (CreateTenantCommandHandler).
///
/// Ver: ADR-Inventory-002 para la política de Container sin UI.
/// </summary>
public class Warehouse : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    // Constructor privado para EF Core
    private Warehouse()
    {
    }

    private Warehouse(
        Guid id,
        Guid tenantId,
        Guid branchId,
        string name,
        string? description,
        bool isDefault) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("El ID del tenant es requerido para crear un almacén.");

        if (branchId == Guid.Empty)
            throw new DomainException("El ID de la sucursal es requerido para crear un almacén.");

        TenantId = tenantId;
        BranchId = branchId;
        SetName(name);
        Description = description?.Trim();
        IsDefault = isDefault;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new WarehouseCreatedDomainEvent(Id, TenantId, BranchId, Name, IsDefault, CreatedAtUtc));
    }

    public static Warehouse Create(
        Guid tenantId,
        Guid branchId,
        string name,
        string? description = null,
        bool isDefault = false)
    {
        return new Warehouse(Guid.NewGuid(), tenantId, branchId, name, description, isDefault);
    }

    /// <summary>
    /// Marca este almacén como el almacén por defecto del tenant.
    /// Solo puede haber un IsDefault=true por tenant — la lógica de unicidad
    /// se garantiza en el caso de uso (application layer), no aquí.
    /// </summary>
    public void SetAsDefault()
    {
        if (IsDefault) return;
        IsDefault = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UnsetDefault()
    {
        IsDefault = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description)
    {
        SetName(name);
        Description = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
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
        if (IsDefault)
            throw new DomainException("No se puede desactivar el almacén predeterminado. Asigne otro almacén por defecto primero.");

        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del almacén no puede estar vacío.");

        string trimmed = name.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 100)
            throw new DomainException("El nombre del almacén debe tener entre 2 y 100 caracteres.");

        Name = trimmed;
    }
}
