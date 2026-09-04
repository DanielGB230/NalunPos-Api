using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para la Transacción Comercial de Venta (POS).
/// </summary>
public class Sale : AggregateRoot<Guid>, ITenantOwnedEntity
{
    private readonly List<SaleLineItem> _lineItems = [];

    public Guid TenantId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public Guid SessionId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public Money SubTotal { get; private set; } = null!;
    public Money TaxAmount { get; private set; } = null!;
    public Money Total { get; private set; } = null!;
    public SaleStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<SaleLineItem> LineItems => _lineItems.AsReadOnly();

    private Sale()
    {
    }

    private Sale(
        Guid id,
        string receiptNumber,
        Guid sessionId,
        Guid? customerId,
        IEnumerable<SaleLineItem> lineItems,
        decimal taxRatePercentage = 0m,
        string currency = "USD") : base(id)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new DomainException("El número de comprobante es requerido.");
        }

        if (sessionId == Guid.Empty)
        {
            throw new DomainException("El ID de la sesión de caja es requerido.");
        }

        var itemsList = lineItems?.ToList() ?? [];
        if (itemsList.Count == 0)
        {
            throw new DomainException("La venta debe contener al menos una línea de producto.");
        }

        ReceiptNumber = receiptNumber.Trim().ToUpperInvariant();
        SessionId = sessionId;
        CustomerId = customerId;
        _lineItems = itemsList;
        Status = SaleStatus.Completed;
        CreatedAtUtc = DateTime.UtcNow;

        CalculateTotals(taxRatePercentage, currency);
    }

    public static Sale Create(
        string receiptNumber,
        Guid sessionId,
        Guid? customerId,
        IEnumerable<SaleLineItem> lineItems,
        decimal taxRatePercentage = 0m,
        string currency = "USD")
    {
        var sale = new Sale(Guid.NewGuid(), receiptNumber, sessionId, customerId, lineItems, taxRatePercentage, currency);
        sale.Complete();
        return sale;
    }

    public void Complete()
    {
        Status = SaleStatus.Completed;

        var eventItems = _lineItems.Select(item => new SaleCompletedItem(
            item.ProductId,
            item.Quantity,
            item.UnitPrice.Amount,
            item.UnitPrice.Currency
        )).ToList();

        RaiseDomainEvent(new SaleCompletedDomainEvent(
            Id,
            ReceiptNumber,
            SessionId,
            CustomerId,
            Total.Amount,
            Total.Currency,
            eventItems,
            CreatedAtUtc));
    }

    public void Cancel()
    {
        if (Status == SaleStatus.Cancelled)
        {
            throw new DomainException("La venta ya ha sido cancelada.");
        }

        Status = SaleStatus.Cancelled;
        RaiseDomainEvent(new SaleCancelledDomainEvent(Id, ReceiptNumber, DateTime.UtcNow));
    }

    private void CalculateTotals(decimal taxRatePercentage, string currency)
    {
        decimal subTotalAmount = _lineItems.Sum(i => i.SubTotal.Amount);
        decimal taxAmountValue = decimal.Round(subTotalAmount * (taxRatePercentage / 100m), 4);
        decimal totalAmountValue = subTotalAmount + taxAmountValue;

        SubTotal = Money.Create(subTotalAmount, currency);
        TaxAmount = Money.Create(taxAmountValue, currency);
        Total = Money.Create(totalAmountValue, currency);
    }
}
