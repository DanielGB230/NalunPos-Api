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
        string fullName,
        TaxId taxId,
        string email,
        string phone,
        Address? address) : base(id)
    {
        SetFullName(fullName);
        TaxId = taxId ?? throw new ArgumentNullException(nameof(taxId));
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Address = address;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new CustomerCreatedDomainEvent(Id, FullName, TaxId.Value, CreatedAtUtc));
    }

    public static Customer Create(
        string fullName,
        TaxId taxId,
        string email = "",
        string phone = "",
        Address? address = null)
    {
        return new Customer(Guid.NewGuid(), fullName, taxId, email, phone, address);
    }

    public static Customer Create(
        Guid id,
        string fullName,
        TaxId taxId,
        string email = "",
        string phone = "",
        Address? address = null)
    {
        return new Customer(id, fullName, taxId, email, phone, address);
    }

    public void UpdateDetails(
        string fullName,
        TaxId taxId,
        string email,
        string phone,
        Address? address)
    {
        SetFullName(fullName);
        TaxId = taxId ?? throw new ArgumentNullException(nameof(taxId));
        Email = email?.Trim() ?? string.Empty;
        Phone = phone?.Trim() ?? string.Empty;
        Address = address;
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new CustomerUpdatedDomainEvent(Id, FullName, UpdatedAtUtc.Value));
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
