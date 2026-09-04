using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Entidad de línea de detalle de compra dentro del agregado Purchase.
/// </summary>
public class PurchaseLineItem : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = null!;
    public Money SubTotal { get; private set; } = null!;

    private PurchaseLineItem()
    {
    }

    internal PurchaseLineItem(
        Guid id,
        Guid productId,
        string productName,
        decimal quantity,
        Money unitPrice) : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("El ID del producto es requerido en la línea de compra.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("El nombre del producto es requerido.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("La cantidad comprada debe ser mayor a cero.");
        }

        ProductId = productId;
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice ?? throw new ArgumentNullException(nameof(unitPrice));
        SubTotal = Money.Create(quantity * unitPrice.Amount, unitPrice.Currency);
    }

    public static PurchaseLineItem Create(
        Guid productId,
        string productName,
        decimal quantity,
        Money unitPrice)
    {
        return new PurchaseLineItem(Guid.NewGuid(), productId, productName, quantity, unitPrice);
    }
}
