using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para Caja Física de punto de venta.
/// </summary>
public class CashRegister : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }          // Sucursal a la que pertenece esta caja
    public string Name { get; private set; } = string.Empty;
    public string SerialNumber { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public Guid? CurrentSessionId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private CashRegister()
    {
    }

    private CashRegister(Guid id, Guid tenantId, Guid branchId, string name, string serialNumber) : base(id)
    {
        TenantId = tenantId;
        BranchId = branchId;
        SetName(name);
        SerialNumber = serialNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static CashRegister Create(Guid tenantId, Guid branchId, string name, string serialNumber = "")
    {
        return new CashRegister(Guid.NewGuid(), tenantId, branchId, name, serialNumber);
    }

    public static CashRegister Create(string name, string serialNumber = "")
    {
        return new CashRegister(Guid.NewGuid(), Guid.Empty, Guid.Empty, name, serialNumber);
    }

    public void UpdateDetails(string name, string serialNumber)
    {
        SetName(name);
        SerialNumber = serialNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetCurrentSession(Guid sessionId)
    {
        if (CurrentSessionId.HasValue && CurrentSessionId.Value != sessionId)
        {
            throw new DomainException($"La caja '{Name}' ya tiene un turno abierto activo.");
        }

        CurrentSessionId = sessionId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ClearCurrentSession()
    {
        CurrentSessionId = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (CurrentSessionId.HasValue)
        {
            throw new DomainException("No se puede desactivar una caja que tiene una sesión abierta.");
        }

        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la caja no puede estar vacío.");
        }

        Name = name.Trim();
    }
}
