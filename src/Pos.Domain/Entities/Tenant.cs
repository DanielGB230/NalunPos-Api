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

    private Tenant(Guid id, string name, CompanyTaxId taxId, TimeProvider timeProvider) : base(id)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        SetName(name);
        TaxId = taxId ?? throw new DomainException("El identificador fiscal (TaxId) es requerido.");
        Status = TenantStatus.PendingProvisioning;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        CreatedAtUtc = utcNow;

        RaiseDomainEvent(new TenantCreatedDomainEvent(Id, Name, TaxId.Value, CreatedAtUtc));
    }

    public static Tenant Create(string name, CompanyTaxId taxId, TimeProvider timeProvider)
    {
        return new Tenant(Guid.CreateVersion7(), name, taxId, timeProvider);
    }

    public static Tenant Create(string name, string taxId, TimeProvider timeProvider)
    {
        return Create(name, new CompanyTaxId(taxId), timeProvider);
    }

    public Result Activate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var transitionError = TenantStatusTransitions.ValidateActivation(Status);
        if (transitionError is not null)
        {
            return Result.Failure(transitionError);
        }

        var oldStatus = Status;
        Status = TenantStatus.Active;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;

        RaiseDomainEvent(new TenantStatusChangedDomainEvent(Id, oldStatus, Status, UpdatedAtUtc.Value));
        return Result.Success();
    }

    public Result Suspend(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var transitionError = TenantStatusTransitions.ValidateSuspension(Status);
        if (transitionError is not null)
        {
            return Result.Failure(transitionError);
        }

        var oldStatus = Status;
        Status = TenantStatus.Suspended;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;

        RaiseDomainEvent(new TenantStatusChangedDomainEvent(Id, oldStatus, Status, UpdatedAtUtc.Value));
        return Result.Success();
    }

    public void UpdateName(string name, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        SetName(name);
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
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
