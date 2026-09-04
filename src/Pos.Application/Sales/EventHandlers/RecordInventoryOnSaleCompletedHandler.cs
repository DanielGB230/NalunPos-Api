using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.Commands;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;

namespace Pos.Application.Sales.EventHandlers;

/// <summary>
/// Domain Event Handler que escucha la confirmación de una Venta (SaleCompletedDomainEvent)
/// y desacopladamente descuenta el stock del Kardex mediante RecordInventoryMovementCommand.
/// </summary>
public sealed class RecordInventoryOnSaleCompletedHandler
    : IDomainEventHandler<SaleCompletedDomainEvent>
{
    private readonly IDispatcher _dispatcher;

    public RecordInventoryOnSaleCompletedHandler(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task HandleAsync(SaleCompletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        foreach (var item in domainEvent.Items)
        {
            var command = new RecordInventoryMovementCommand(
                item.ProductId,
                -item.Quantity,
                InventoryMovementType.Sale,
                domainEvent.SaleId,
                $"Salida por Venta N° {domainEvent.ReceiptNumber}"
            );

            await _dispatcher.SendAsync(command, cancellationToken);
        }
    }
}
