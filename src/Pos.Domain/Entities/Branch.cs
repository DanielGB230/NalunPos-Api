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
        string phoneNumber) : this(id, tenantId, name, address, phoneNumber, null)
    {
    }

    private Branch(
        Guid id,
        Guid tenantId,
        string name,
        Address address,
        string phoneNumber,
        TimeProvider? timeProvider) : base(id)
    {
        TenantId = TenantGuard.RequireTenantId(tenantId, "una sucursal");
        SetName(name);
        Address = address ?? throw new ArgumentNullException(nameof(address));
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        IsActive = true;
        CreatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new BranchCreatedDomainEvent(Id, Name, CreatedAtUtc));
    }

    public static Branch Create(Guid tenantId, string name, Address address, string phoneNumber = "", TimeProvider? timeProvider = null)
    {
        return new Branch(Guid.NewGuid(), tenantId, name, address, phoneNumber, timeProvider);
    }


    public void UpdateDetails(string name, Address address, string phoneNumber, TimeProvider? timeProvider = null)
    {
        SetName(name);
        Address = address ?? throw new ArgumentNullException(nameof(address));
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new BranchUpdatedDomainEvent(Id, Name, UpdatedAtUtc.Value));
    }

    public void Activate(TimeProvider? timeProvider = null)
    {
        IsActive = true;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    public void Deactivate(TimeProvider? timeProvider = null)
    {
        IsActive = false;
        UpdatedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
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
