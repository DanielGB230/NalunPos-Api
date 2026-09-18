using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para Producto en el Catálogo Comercial.
/// Encapsulamiento estricto: setters privados, mutación mediante métodos de negocio.
/// </summary>
public class Product : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Sku Sku { get; private set; } = null!;
    public Barcode? Barcode { get; private set; }
    public Money Price { get; private set; } = null!;
    public Money? Cost { get; private set; }
    public UnitOfMeasure UnitOfMeasure { get; private set; } = UnitOfMeasure.Unit;
    public Guid CategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    // Constructor privado para EF Core
    private Product()
    {
    }

    private Product(
        Guid id,
        string name,
        Sku sku,
        Money price,
        Guid categoryId,
        string? description = null,
        Barcode? barcode = null,
        Money? cost = null,
        UnitOfMeasure unitOfMeasure = UnitOfMeasure.Unit) : base(id)
    {
        SetName(name);
        Sku = sku ?? throw new ArgumentNullException(nameof(sku));
        Price = price ?? throw new ArgumentNullException(nameof(price));
        CategoryId = categoryId != Guid.Empty ? categoryId : throw new DomainException("El ID de la categoría debe ser válido.");
        Description = description?.Trim();
        Barcode = barcode;
        Cost = cost;
        UnitOfMeasure = unitOfMeasure;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new ProductCreatedDomainEvent(
            Id,
            Name,
            Sku.Value,
            Price.Amount,
            Price.Currency,
            CategoryId,
            CreatedAtUtc));
    }

    public static Product Create(
        string name,
        Sku sku,
        Money price,
        Guid categoryId,
        string? description = null,
        Barcode? barcode = null,
        Money? cost = null,
        UnitOfMeasure unitOfMeasure = UnitOfMeasure.Unit)
    {
        return new Product(Guid.NewGuid(), name, sku, price, categoryId, description, barcode, cost, unitOfMeasure);
    }

    public static Product Create(
        Guid id,
        string name,
        Sku sku,
        Money price,
        Guid categoryId,
        string? description = null,
        Barcode? barcode = null,
        Money? cost = null,
        UnitOfMeasure unitOfMeasure = UnitOfMeasure.Unit)
    {
        return new Product(id, name, sku, price, categoryId, description, barcode, cost, unitOfMeasure);
    }

    public void UpdateDetails(string name, string? description, Barcode? barcode)
    {
        SetName(name);
        Description = description?.Trim();
        Barcode = barcode;
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new ProductUpdatedDomainEvent(Id, Name, UpdatedAtUtc.Value));
    }

    public void UpdatePrice(Money newPrice, Money? newCost = null)
    {
        ArgumentNullException.ThrowIfNull(newPrice);

        if (Price.Amount == newPrice.Amount && Price.Currency == newPrice.Currency && Cost == newCost)
        {
            return;
        }

        var oldPrice = Price;
        Price = newPrice;
        if (newCost != null)
        {
            Cost = newCost;
        }
        UpdatedAtUtc = DateTime.UtcNow;

        RaiseDomainEvent(new ProductPriceUpdatedDomainEvent(
            Id,
            oldPrice.Amount,
            newPrice.Amount,
            newPrice.Currency,
            UpdatedAtUtc.Value));
    }

    public void ChangeCategory(Guid newCategoryId)
    {
        if (newCategoryId == Guid.Empty)
        {
            throw new DomainException("El ID de la categoría no es válido.");
        }

        if (CategoryId == newCategoryId) return;

        CategoryId = newCategoryId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ChangeUnitOfMeasure(UnitOfMeasure unitOfMeasure)
    {
        if (UnitOfMeasure == unitOfMeasure) return;
        UnitOfMeasure = unitOfMeasure;
        UpdatedAtUtc = DateTime.UtcNow;
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

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del producto no puede estar vacío.");
        }

        string trimmedName = name.Trim();
        if (trimmedName.Length < 2 || trimmedName.Length > 150)
        {
            throw new DomainException("El nombre del producto debe contener entre 2 y 150 caracteres.");
        }

        Name = trimmedName;
    }
}
