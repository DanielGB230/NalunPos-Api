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

    private CashRegister(Guid id, Guid tenantId, Guid branchId, string name, string serialNumber, TimeProvider? timeProvider = null) : base(id)
    {
        TenantId = TenantGuard.RequireTenantId(tenantId, "una caja");

        if (branchId == Guid.Empty)
            throw new DomainException("El ID de la sucursal es requerido para crear una caja.");
        BranchId = branchId;
        SetName(name);
        SerialNumber = serialNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        IsActive = true;
        CreatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public static CashRegister Create(Guid tenantId, Guid branchId, string name, string serialNumber = "", TimeProvider? timeProvider = null)
    {
        return new CashRegister(Guid.NewGuid(), tenantId, branchId, name, serialNumber, timeProvider);
    }

    public void UpdateDetails(string name, string serialNumber, TimeProvider? timeProvider = null)
    {
        SetName(name);
        SerialNumber = serialNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void SetCurrentSession(Guid sessionId, TimeProvider? timeProvider = null)
    {
        if (CurrentSessionId.HasValue && CurrentSessionId.Value != sessionId)
        {
            throw new DomainException($"La caja '{Name}' ya tiene un turno abierto activo.");
        }

        CurrentSessionId = sessionId;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void ClearCurrentSession(TimeProvider? timeProvider = null)
    {
        CurrentSessionId = null;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void Activate(TimeProvider? timeProvider = null)
    {
        IsActive = true;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void Deactivate(TimeProvider? timeProvider = null)
    {
        if (CurrentSessionId.HasValue)
        {
            throw new DomainException("No se puede desactivar una caja que tiene una sesión abierta.");
        }

        IsActive = false;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
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
