using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Entidad de detalle / línea de venta dentro del agregado Sale.
/// </summary>
public class SaleLineItem : Entity<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public Money SubTotal { get; private set; } = null!;

    private SaleLineItem()
    {
    }

    internal SaleLineItem(
        Guid id,
        Guid productId,
        string productName,
        decimal quantity,
        Money unitPrice) : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("El ID del producto es requerido en la línea de venta.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("El nombre del producto es requerido.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("La cantidad en la línea de venta debe ser mayor a cero.");
        }

        ProductId = productId;
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice ?? throw new ArgumentNullException(nameof(unitPrice));
        SubTotal = Money.Create(quantity * unitPrice.Amount, unitPrice.Currency);
    }

    public static SaleLineItem Create(
        Guid productId,
        string productName,
        decimal quantity,
        Money unitPrice)
    {
        return new SaleLineItem(Guid.NewGuid(), productId, productName, quantity, unitPrice);
    }
}
