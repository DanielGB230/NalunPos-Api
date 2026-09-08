using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para Sucursal física del negocio.
/// </summary>
public class Branch : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Address Address { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Branch()
    {
    }

    private Branch(
        Guid id,
        Guid tenantId,
        string name,
        Address address,
        string phoneNumber) : base(id)
    {
        TenantId = tenantId;
        SetName(name);
        Address = address ?? throw new ArgumentNullException(nameof(address));
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new BranchCreatedDomainEvent(Id, Name, CreatedAtUtc));
    }

    public static Branch Create(Guid tenantId, string name, Address address, string phoneNumber = "")
    {
        return new Branch(Guid.NewGuid(), tenantId, name, address, phoneNumber);
    }

    public static Branch Create(string name, Address address, string phoneNumber = "")
    {
        return new Branch(Guid.NewGuid(), Guid.Empty, name, address, phoneNumber);
    }

    public void UpdateDetails(string name, Address address, string phoneNumber)
    {
        SetName(name);
        Address = address ?? throw new ArgumentNullException(nameof(address));
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new BranchUpdatedDomainEvent(Id, Name, UpdatedAtUtc.Value));
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
            throw new DomainException("El nombre de la sucursal no puede estar vacío.");
        }

        Name = name.Trim();
    }
}
