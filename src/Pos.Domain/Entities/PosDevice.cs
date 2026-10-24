using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para Dispositivo o Terminal POS físico registrado en una Sucursal.
/// </summary>
public class PosDevice : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string SerialNumber { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastPingUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private PosDevice()
    {
    }

    private PosDevice(
        Guid id,
        Guid tenantId,
        Guid branchId,
        string name,
        string serialNumber,
        TimeProvider timeProvider) : base(id)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        TenantId = TenantGuard.RequireTenantId(tenantId, "un dispositivo POS");

        if (branchId == Guid.Empty)
        {
            throw new DomainException("El ID de la sucursal asignada es requerido.");
        }

        SetName(name);
        SetSerialNumber(serialNumber);

        BranchId = branchId;
        IsActive = true;
        CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new PosDeviceRegisteredDomainEvent(Id, BranchId, Name, SerialNumber, CreatedAtUtc));
    }

    public static PosDevice Create(Guid tenantId, Guid branchId, string name, string serialNumber, TimeProvider timeProvider)
    {
        return new PosDevice(Guid.NewGuid(), tenantId, branchId, name, serialNumber, timeProvider);
    }

    public void UpdateDetails(string name, string serialNumber, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        SetName(name);
        SetSerialNumber(serialNumber);
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void RecordPing(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var utcNowOffset = timeProvider.GetUtcNow();
        LastPingUtc = utcNowOffset;
        RaiseDomainEvent(new PosDevicePingedDomainEvent(Id, LastPingUtc.Value, utcNowOffset.UtcDateTime));
    }

    public void Activate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        IsActive = true;
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void Deactivate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        IsActive = false;
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del dispositivo no puede estar vacío.");
        }
        Name = name.Trim();
    }

    private void SetSerialNumber(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new DomainException("El número de serie o MAC del dispositivo es requerido.");
        }
        SerialNumber = serialNumber.Trim().ToUpperInvariant();
    }
}
