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
        Guid tenantId,
        string name,
        Sku sku,
        Money price,
        Guid categoryId,
        string? description,
        Barcode? barcode,
        Money? cost,
        UnitOfMeasure unitOfMeasure,
        TimeProvider timeProvider) : base(id)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        TenantId = TenantGuard.RequireTenantId(tenantId, "un producto");
        SetName(name);
        Sku = sku ?? throw new ArgumentNullException(nameof(sku));
        Price = price ?? throw new ArgumentNullException(nameof(price));
        CategoryId = categoryId != Guid.Empty ? categoryId : throw new DomainException("El ID de la categoría debe ser válido.");
        Description = description?.Trim();
        Barcode = barcode;
        Cost = cost;
        UnitOfMeasure = unitOfMeasure;
        IsActive = true;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        CreatedAtUtc = utcNow;

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
        Guid tenantId,
        string name,
        Sku sku,
        Money price,
        Guid categoryId,
        TimeProvider timeProvider)
    {
        return Create(tenantId, name, sku, price, categoryId, null, null, null, UnitOfMeasure.Unit, timeProvider);
    }

    public static Product Create(
        Guid tenantId,
        string name,
        Sku sku,
        Money price,
        Guid categoryId,
        string? description,
        Barcode? barcode,
        Money? cost,
        TimeProvider timeProvider)
    {
        return Create(tenantId, name, sku, price, categoryId, description, barcode, cost, UnitOfMeasure.Unit, timeProvider);
    }

    public static Product Create(
        Guid tenantId,
        string name,
        Sku sku,
        Money price,
        Guid categoryId,
        string? description,
        Barcode? barcode,
        Money? cost,
        UnitOfMeasure unitOfMeasure,
        TimeProvider timeProvider)
    {
        return new Product(Guid.NewGuid(), tenantId, name, sku, price, categoryId, description, barcode, cost, unitOfMeasure, timeProvider);
    }

    public void UpdateDetails(string name, TimeProvider timeProvider)
    {
        UpdateDetails(name, null, null, timeProvider);
    }

    public void UpdateDetails(string name, string? description, Barcode? barcode, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        SetName(name);
        Description = description?.Trim();
        Barcode = barcode;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;

        RaiseDomainEvent(new ProductUpdatedDomainEvent(Id, Name, UpdatedAtUtc.Value));
    }

    public void UpdatePrice(Money newPrice, TimeProvider timeProvider)
    {
        UpdatePrice(newPrice, null, timeProvider);
    }

    public void UpdatePrice(Money newPrice, Money? newCost, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(newPrice);
        ArgumentNullException.ThrowIfNull(timeProvider);

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
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;

        RaiseDomainEvent(new ProductPriceUpdatedDomainEvent(
            Id,
            oldPrice.Amount,
            newPrice.Amount,
            newPrice.Currency,
            UpdatedAtUtc.Value));
    }

    public void ChangeCategory(Guid newCategoryId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (newCategoryId == Guid.Empty)
        {
            throw new DomainException("El ID de la categoría no es válido.");
        }

        if (CategoryId == newCategoryId) return;

        CategoryId = newCategoryId;
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void ChangeUnitOfMeasure(UnitOfMeasure unitOfMeasure, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (UnitOfMeasure == unitOfMeasure) return;
        UnitOfMeasure = unitOfMeasure;
        UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public void Activate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (IsActive) return;
        IsActive = true;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new ProductStatusChangedDomainEvent(Id, TenantId, IsActive, UpdatedAtUtc.Value));
    }

    public void Deactivate(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (!IsActive) return;
        IsActive = false;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new ProductStatusChangedDomainEvent(Id, TenantId, IsActive, UpdatedAtUtc.Value));
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
