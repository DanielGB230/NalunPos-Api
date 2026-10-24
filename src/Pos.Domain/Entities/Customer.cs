using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para Cliente del POS.
/// Encapsulamiento estricto: setters privados, mutación mediante métodos de negocio.
/// </summary>
public class Customer : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public TaxId TaxId { get; private set; } = null!;
    public Address? Address { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    private Customer()
    {
    }

    private Customer(
        Guid id,
        Guid tenantId,
        string fullName,
        TaxId taxId,
        string email,
        string phone,
        Address? address,
        TimeProvider timeProvider) : base(id)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        TenantId = TenantGuard.RequireTenantId(tenantId, "un cliente");
        SetFullName(fullName);
        TaxId = taxId ?? throw new ArgumentNullException(nameof(taxId));
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Address = address;
        IsActive = true;
        CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new CustomerCreatedDomainEvent(Id, FullName, TaxId.Value, CreatedAtUtc));
    }

    public static Customer Create(
        Guid tenantId,
        string fullName,
        TaxId taxId,
        TimeProvider timeProvider)
    {
        return Create(tenantId, fullName, taxId, string.Empty, string.Empty, null, timeProvider);
    }

    public static Customer Create(
        Guid tenantId,
        string fullName,
        TaxId taxId,
        string email,
        string phone,
        Address? address,
        TimeProvider timeProvider)
    {
        return new Customer(Guid.NewGuid(), tenantId, fullName, taxId, email, phone, address, timeProvider);
    }

    public void UpdateDetails(
        string fullName,
        TaxId taxId,
        string email,
        string phone,
        Address? address,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        SetFullName(fullName);
        TaxId = taxId ?? throw new ArgumentNullException(nameof(taxId));
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Address = address;
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new CustomerUpdatedDomainEvent(Id, FullName, UpdatedAtUtc.Value));
    }

    public void Activate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (IsActive) return;
        IsActive = true;
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void Deactivate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (!IsActive) return;
        IsActive = false;
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    private void SetFullName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre completo del cliente es requerido.");
        }

        string trimmed = name.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 150)
        {
            throw new DomainException("El nombre del cliente debe tener entre 2 y 150 caracteres.");
        }

        FullName = trimmed;
    }
}
