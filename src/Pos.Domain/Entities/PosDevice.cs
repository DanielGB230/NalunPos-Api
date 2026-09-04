using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para Dispositivo o Terminal POS físico registrado en una Sucursal.
/// </summary>
public class PosDevice : AggregateRoot<Guid>
{
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
        Guid branchId,
        string name,
        string serialNumber) : base(id)
    {
        if (branchId == Guid.Empty)
        {
            throw new DomainException("El ID de la sucursal asignada es requerido.");
        }

        SetName(name);
        SetSerialNumber(serialNumber);

        BranchId = branchId;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new PosDeviceRegisteredDomainEvent(Id, BranchId, Name, SerialNumber, CreatedAtUtc));
    }

    public static PosDevice Create(Guid branchId, string name, string serialNumber)
    {
        return new PosDevice(Guid.NewGuid(), branchId, name, serialNumber);
    }

    public void UpdateDetails(string name, string serialNumber)
    {
        SetName(name);
        SetSerialNumber(serialNumber);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordPing()
    {
        LastPingUtc = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PosDevicePingedDomainEvent(Id, LastPingUtc.Value, DateTime.UtcNow));
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
