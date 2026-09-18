using Pos.Domain.Common;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Línea de detalle de una Orden de Compra (owned entity del agregado PurchaseOrder).
///
/// QuantityReceived se incrementa a medida que llega mercadería (recepciones parciales).
/// QuantityPending = QuantityOrdered - QuantityReceived.
/// </summary>
public class PurchaseOrderLine : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public decimal QuantityOrdered { get; private set; }
    public decimal QuantityReceived { get; private set; }
    public Money UnitCost { get; private set; } = null!;

    /// <summary>Cantidad pendiente de recibir.</summary>
    public decimal QuantityPending => QuantityOrdered - QuantityReceived;

    /// <summary>True cuando la línea fue recibida completamente.</summary>
    public bool IsFullyReceived => QuantityReceived >= QuantityOrdered;

    /// <summary>Costo total de la línea según lo pedido.</summary>
    public Money TotalCost => Money.Create(QuantityOrdered * UnitCost.Amount, UnitCost.Currency);

    // Constructor privado para EF Core
    private PurchaseOrderLine()
    {
    }

    private PurchaseOrderLine(
        Guid id,
        Guid productId,
        decimal quantityOrdered,
        Money unitCost) : base(id)
    {
        if (productId == Guid.Empty)
            throw new DomainException("El ID del producto es requerido en una línea de orden de compra.");

        if (quantityOrdered <= 0)
            throw new DomainException("La cantidad ordenada debe ser mayor a cero.");

        ProductId = productId;
        QuantityOrdered = quantityOrdered;
        QuantityReceived = 0m;
        UnitCost = unitCost ?? throw new ArgumentNullException(nameof(unitCost));
    }

    internal static PurchaseOrderLine Create(
        Guid productId,
        decimal quantityOrdered,
        Money unitCost)
    {
        return new PurchaseOrderLine(Guid.NewGuid(), productId, quantityOrdered, unitCost);
    }

    /// <summary>
    /// Registra mercadería recibida en esta línea (total o parcial).
    /// No puede exceder la cantidad ordenada.
    /// </summary>
    internal void AddReceivedQuantity(decimal quantity)
    {
        if (quantity <= 0)
            throw new DomainException("La cantidad recibida debe ser mayor a cero.");

        if (QuantityReceived + quantity > QuantityOrdered)
            throw new DomainException(
                $"La cantidad recibida ({QuantityReceived + quantity}) supera la cantidad ordenada ({QuantityOrdered}) para el producto '{ProductId}'.");

        QuantityReceived += quantity;
    }
}
