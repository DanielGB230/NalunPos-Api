using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para la Orden de Compra a Proveedores.
/// </summary>
public class Purchase : AggregateRoot<Guid>, ITenantOwnedEntity
{
    private readonly List<PurchaseLineItem> _lineItems = [];

    public Guid TenantId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public Money TotalAmount { get; private set; } = null!;
    public PurchaseStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<PurchaseLineItem> LineItems => _lineItems.AsReadOnly();

    private Purchase()
    {
    }

    private Purchase(
        Guid id,
        Guid supplierId,
        string orderNumber,
        IEnumerable<PurchaseLineItem> lineItems,
        string currency = "USD") : base(id)
    {
        if (supplierId == Guid.Empty)
        {
            throw new DomainException("El ID del proveedor es requerido.");
        }

        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("El número de orden de compra es requerido.");
        }

        var itemsList = lineItems?.ToList() ?? [];
        if (itemsList.Count == 0)
        {
            throw new DomainException("La orden de compra debe contener al menos un producto.");
        }

        SupplierId = supplierId;
        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        _lineItems = itemsList;
        Status = PurchaseStatus.Draft;
        CreatedAtUtc = DateTime.UtcNow;

        decimal totalValue = _lineItems.Sum(i => i.SubTotal.Amount);
        TotalAmount = Money.Create(totalValue, currency);
    }

    public static Purchase Create(
        Guid supplierId,
        string orderNumber,
        IEnumerable<PurchaseLineItem> lineItems,
        string currency = "USD")
    {
        return new Purchase(Guid.NewGuid(), supplierId, orderNumber, lineItems, currency);
    }

    public void Complete()
    {
        if (Status == PurchaseStatus.Completed)
        {
            throw new DomainException("La orden de compra ya ha sido completada.");
        }

        if (Status == PurchaseStatus.Cancelled)
        {
            throw new DomainException("No se puede completar una orden de compra cancelada.");
        }

        Status = PurchaseStatus.Completed;

        var eventItems = _lineItems.Select(item => new PurchaseCompletedItem(
            item.ProductId,
            item.Quantity,
            item.UnitPrice.Amount,
            item.UnitPrice.Currency
        )).ToList();

        RaiseDomainEvent(new PurchaseCompletedDomainEvent(
            Id,
            SupplierId,
            OrderNumber,
            TotalAmount.Amount,
            TotalAmount.Currency,
            eventItems,
            DateTime.UtcNow));
    }

    public void Cancel()
    {
        if (Status == PurchaseStatus.Cancelled)
        {
            throw new DomainException("La orden de compra ya se encuentra cancelada.");
        }

        if (Status == PurchaseStatus.Completed)
        {
            throw new DomainException("No se puede cancelar una orden de compra que ya ha sido completada e ingresada al almacén.");
        }

        Status = PurchaseStatus.Cancelled;
        RaiseDomainEvent(new PurchaseCancelledDomainEvent(Id, OrderNumber, DateTime.UtcNow));
    }
}
