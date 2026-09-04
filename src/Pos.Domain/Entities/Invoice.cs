using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para el Comprobante de Facturación Electrónica (Factura/Boleta).
/// </summary>
public class Invoice : AggregateRoot<Guid>
{
    public Guid SaleId { get; private set; }
    public InvoiceDocumentType DocumentType { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public TaxId CustomerTaxId { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;
    public DateTime IssueDateUtc { get; private set; }
    public InvoiceStatus Status { get; private set; }

    private Invoice()
    {
    }

    private Invoice(
        Guid id,
        Guid saleId,
        InvoiceDocumentType documentType,
        string documentNumber,
        TaxId customerTaxId,
        Money totalAmount) : base(id)
    {
        if (saleId == Guid.Empty)
        {
            throw new DomainException("El ID de la venta es requerido para emitir la factura.");
        }

        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            throw new DomainException("El número de documento de facturación es requerido.");
        }

        SaleId = saleId;
        DocumentType = documentType;
        DocumentNumber = documentNumber.Trim().ToUpperInvariant();
        CustomerTaxId = customerTaxId ?? throw new ArgumentNullException(nameof(customerTaxId));
        TotalAmount = totalAmount ?? throw new ArgumentNullException(nameof(totalAmount));
        Status = InvoiceStatus.Pending;
        IssueDateUtc = DateTime.UtcNow;

        RaiseDomainEvent(new InvoiceIssuedDomainEvent(
            Id,
            SaleId,
            DocumentNumber,
            DocumentType.ToString(),
            TotalAmount.Amount,
            TotalAmount.Currency,
            IssueDateUtc));
    }

    public static Invoice Create(
        Guid saleId,
        InvoiceDocumentType documentType,
        string documentNumber,
        TaxId customerTaxId,
        Money totalAmount)
    {
        return new Invoice(Guid.NewGuid(), saleId, documentType, documentNumber, customerTaxId, totalAmount);
    }

    public void MarkAsSent()
    {
        Status = InvoiceStatus.Sent;
        RaiseDomainEvent(new InvoiceStatusUpdatedDomainEvent(Id, DocumentNumber, Status.ToString(), DateTime.UtcNow));
    }

    public void MarkAsAccepted()
    {
        Status = InvoiceStatus.Accepted;
        RaiseDomainEvent(new InvoiceStatusUpdatedDomainEvent(Id, DocumentNumber, Status.ToString(), DateTime.UtcNow));
    }

    public void MarkAsRejected()
    {
        Status = InvoiceStatus.Rejected;
        RaiseDomainEvent(new InvoiceStatusUpdatedDomainEvent(Id, DocumentNumber, Status.ToString(), DateTime.UtcNow));
    }
}
