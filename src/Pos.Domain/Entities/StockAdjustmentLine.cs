using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Línea de ajuste de stock (owned entity del agregado StockAdjustment).
/// Quantity positivo = entrada, negativo = salida.
/// </summary>
public class StockAdjustmentLine : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }   // +/- según tipo de ajuste

    // Constructor privado para EF Core
    private StockAdjustmentLine()
    {
    }

    private StockAdjustmentLine(Guid id, Guid productId, decimal quantity) : base(id)
    {
        if (productId == Guid.Empty)
            throw new DomainException("El ID del producto es requerido en una línea de ajuste.");

        if (quantity == 0)
            throw new DomainException("La cantidad del ajuste no puede ser cero.");

        ProductId = productId;
        Quantity = quantity;
    }

    internal static StockAdjustmentLine Create(Guid productId, decimal quantity)
    {
        return new StockAdjustmentLine(Guid.NewGuid(), productId, quantity);
    }

    /// <summary>True si el ajuste es una entrada (cantidad positiva).</summary>
    public bool IsEntry => Quantity > 0;

    /// <summary>True si el ajuste es una salida (cantidad negativa).</summary>
    public bool IsExit => Quantity < 0;
}
