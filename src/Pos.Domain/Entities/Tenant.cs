using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para Inquilino / Organización (Tenant).
/// Representa la empresa cliente en el sistema POS SaaS Multi-tenant.
/// NO implementa ITenantOwnedEntity (un Tenant no pertenece a un tenant, es la raíz del aislamiento).
/// </summary>
public class Tenant : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public CompanyTaxId TaxId { get; private set; } = null!;
    public TenantStatus Status { get; private set; } = TenantStatus.PendingProvisioning;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Tenant()
    {
    }

    private Tenant(Guid id, string name, CompanyTaxId taxId) : base(id)
    {
        SetName(name);
        TaxId = taxId ?? throw new DomainException("El identificador fiscal (TaxId) es requerido.");
        Status = TenantStatus.PendingProvisioning;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new TenantCreatedDomainEvent(Id, Name, TaxId.Value, CreatedAtUtc));
    }

    public static Tenant Create(string name, CompanyTaxId taxId)
    {
        return new Tenant(Guid.CreateVersion7(), name, taxId);
    }

    public static Tenant Create(string name, string taxId)
    {
        return Create(name, new CompanyTaxId(taxId));
    }

    public void Activate()
    {
        if (Status == TenantStatus.Active)
        {
            return;
        }

        Status = TenantStatus.Active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Suspend()
    {
        if (Status == TenantStatus.Suspended)
        {
            return;
        }

        Status = TenantStatus.Suspended;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateName(string name)
    {
        SetName(name);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del tenant es requerido.");
        }
        Name = name.Trim();
    }
}
