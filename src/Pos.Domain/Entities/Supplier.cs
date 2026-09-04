using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para Proveedor de productos / mercadería.
/// Encapsulamiento estricto: setters privados, mutación únicamente mediante métodos explícitos.
/// </summary>
public class Supplier : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ContactName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public TaxId TaxId { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    // Constructor privado para EF Core
    private Supplier()
    {
    }

    private Supplier(
        Guid id,
        string name,
        TaxId taxId,
        Address address,
        string contactName,
        string email,
        string phone) : base(id)
    {
        SetName(name);
        TaxId = taxId ?? throw new ArgumentNullException(nameof(taxId));
        Address = address ?? throw new ArgumentNullException(nameof(address));
        SetContactDetails(contactName, email, phone);
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new SupplierCreatedDomainEvent(Id, Name, TaxId.Value, CreatedAtUtc));
    }

    public static Supplier Create(
        string name,
        TaxId taxId,
        Address address,
        string contactName,
        string email,
        string phone)
    {
        return new Supplier(Guid.NewGuid(), name, taxId, address, contactName, email, phone);
    }

    public static Supplier Create(
        Guid id,
        string name,
        TaxId taxId,
        Address address,
        string contactName,
        string email,
        string phone)
    {
        return new Supplier(id, name, taxId, address, contactName, email, phone);
    }

    public void UpdateDetails(
        string name,
        TaxId taxId,
        Address address,
        string contactName,
        string email,
        string phone)
    {
        SetName(name);
        TaxId = taxId ?? throw new ArgumentNullException(nameof(taxId));
        Address = address ?? throw new ArgumentNullException(nameof(address));
        SetContactDetails(contactName, email, phone);
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new SupplierUpdatedDomainEvent(Id, Name, UpdatedAtUtc.Value));
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new SupplierDeactivatedDomainEvent(Id, UpdatedAtUtc.Value));
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del proveedor es requerido.");
        }

        string trimmed = name.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 150)
        {
            throw new DomainException("El nombre del proveedor debe tener entre 2 y 150 caracteres.");
        }

        Name = trimmed;
    }

    private void SetContactDetails(string contactName, string email, string phone)
    {
        ContactName = contactName?.Trim() ?? string.Empty;
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
    }
}
