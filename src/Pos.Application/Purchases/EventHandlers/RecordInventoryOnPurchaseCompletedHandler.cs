using Pos.Application.Common.Interfaces;
using Pos.Application.Inventory.Commands;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;

namespace Pos.Application.Purchases.EventHandlers;

/// <summary>
/// Domain Event Handler que escucha la finalización de una Orden de Compra y
/// desacopladamente incrementa el stock Kardex mediante RecordInventoryMovementCommand.
/// </summary>
public sealed class RecordInventoryOnPurchaseCompletedHandler
    : IDomainEventHandler<PurchaseCompletedDomainEvent>
{
    private readonly IDispatcher _dispatcher;

    public RecordInventoryOnPurchaseCompletedHandler(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task HandleAsync(PurchaseCompletedDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        foreach (var item in domainEvent.Items)
        {
            var command = new RecordInventoryMovementCommand(
                item.ProductId,
                item.Quantity,
                InventoryMovementType.Purchase,
                domainEvent.PurchaseId,
                $"Ingreso por Compra N° {domainEvent.OrderNumber}"
            );

            await _dispatcher.SendAsync(command, cancellationToken);
        }
    }
}
