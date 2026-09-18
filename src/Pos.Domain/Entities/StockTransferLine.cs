using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Entities;

/// <summary>
/// Línea de traspaso entre almacenes (owned entity del agregado StockTransfer).
/// La cantidad siempre es positiva — la dirección (salida/entrada) la define el almacén.
/// </summary>
public class StockTransferLine : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }   // Siempre > 0

    // Constructor privado para EF Core
    private StockTransferLine()
    {
    }

    private StockTransferLine(Guid id, Guid productId, decimal quantity) : base(id)
    {
        if (productId == Guid.Empty)
            throw new DomainException("El ID del producto es requerido en una línea de traspaso.");

        if (quantity <= 0)
            throw new DomainException("La cantidad a traspasar debe ser mayor a cero.");

        ProductId = productId;
        Quantity = quantity;
    }

    internal static StockTransferLine Create(Guid productId, decimal quantity)
    {
        return new StockTransferLine(Guid.NewGuid(), productId, quantity);
    }
}
