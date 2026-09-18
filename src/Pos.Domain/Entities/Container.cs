using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Ubicación interna dentro de un Warehouse (estante, bin, contenedor físico).
///
/// NOTA — ADR-Inventory-002:
/// Esta entidad existe en el modelo de datos desde la fase inicial para que
/// StockLevel e InventoryMovement puedan referenciar ContainerId (nullable)
/// sin requerir una migración de esquema posterior.
///
/// La interfaz de usuario para gestión de Containers NO SE CONSTRUYE en esta fase.
/// Disparador para construir la UI: cuando un tenant real lo solicite o cuando
/// el volumen de un almacén justifique gestión de ubicaciones internas.
/// </summary>
public class Container : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? ParentContainerId { get; private set; }    // Jerarquía: Depósito A > Estante 3 > Bin 12
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    // Constructor privado para EF Core
    private Container()
    {
    }

    private Container(
        Guid id,
        Guid tenantId,
        Guid warehouseId,
        string name,
        Guid? parentContainerId) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new DomainException("El ID del tenant es requerido para crear un contenedor.");

        if (warehouseId == Guid.Empty)
            throw new DomainException("El ID del almacén es requerido para crear un contenedor.");

        TenantId = tenantId;
        WarehouseId = warehouseId;
        SetName(name);
        ParentContainerId = parentContainerId;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Container Create(
        Guid tenantId,
        Guid warehouseId,
        string name,
        Guid? parentContainerId = null)
    {
        return new Container(Guid.NewGuid(), tenantId, warehouseId, name, parentContainerId);
    }

    public bool IsRootContainer => ParentContainerId is null;

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del contenedor no puede estar vacío.");

        Name = name.Trim();
    }
}
