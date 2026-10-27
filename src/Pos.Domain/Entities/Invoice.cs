using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado Raíz para el Comprobante de Facturación Electrónica (Factura/Boleta).
/// </summary>
public class Invoice : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid SaleId { get; private set; }
    public InvoiceDocumentType DocumentType { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public TaxId CustomerTaxId { get; private set; } = null!;
    public Money TotalAmount { get; private set; } = null!;
    public DateTime IssueDateUtc { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
    public int ReconciliationAttempts { get; private set; }
    public string? ManualInterventionReason { get; private set; }
    public DateTime? ManualInterventionAtUtc { get; private set; }

    private Invoice()
    {
    }

    private Invoice(
        Guid id,
        Guid tenantId,
        Guid saleId,
        InvoiceDocumentType documentType,
        string documentNumber,
        TaxId customerTaxId,
        Money totalAmount,
        TimeProvider timeProvider) : base(id)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        TenantId = TenantGuard.RequireTenantId(tenantId, "una factura");

        if (saleId == Guid.Empty)
        {
            throw new DomainException("El ID de la venta es requerido para emitir la factura.");
        }

        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            throw new DomainException("El número de documento de facturación es requerido.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        SaleId = saleId;
        DocumentType = documentType;
        DocumentNumber = documentNumber.Trim().ToUpperInvariant();
        CustomerTaxId = customerTaxId ?? throw new ArgumentNullException(nameof(customerTaxId));
        TotalAmount = totalAmount ?? throw new ArgumentNullException(nameof(totalAmount));
        Status = InvoiceStatus.Pending;
        IssueDateUtc = utcNow;

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
        Guid tenantId,
        Guid saleId,
        InvoiceDocumentType documentType,
        string documentNumber,
        TaxId customerTaxId,
        Money totalAmount,
        TimeProvider timeProvider)
    {
        return new Invoice(Guid.NewGuid(), tenantId, saleId, documentType, documentNumber, customerTaxId, totalAmount, timeProvider);
    }

    public void MarkAsSent(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        Status = InvoiceStatus.Sent;
        ManualInterventionReason = null;
        ManualInterventionAtUtc = null;
        RaiseDomainEvent(new InvoiceStatusUpdatedDomainEvent(Id, DocumentNumber, Status.ToString(), utcNow));
    }

    public void MarkAsAccepted(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        Status = InvoiceStatus.Accepted;
        ManualInterventionReason = null;
        ManualInterventionAtUtc = null;
        RaiseDomainEvent(new InvoiceStatusUpdatedDomainEvent(Id, DocumentNumber, Status.ToString(), utcNow));
    }

    public void MarkAsRejected(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        Status = InvoiceStatus.Rejected;
        ManualInterventionReason = null;
        ManualInterventionAtUtc = null;
        RaiseDomainEvent(new InvoiceStatusUpdatedDomainEvent(Id, DocumentNumber, Status.ToString(), utcNow));
    }

    public void IncrementReconciliationAttempts()
    {
        ReconciliationAttempts++;
    }

    public void MarkAsRequiresManualIntervention(string reason, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("El motivo de la intervención manual es requerido.");
        }

        var trimmed = reason.Trim();
        if (trimmed.Length > InvoiceConstants.MaxManualInterventionReasonLength)
        {
            throw new DomainException($"El motivo de la intervención manual no puede superar los {InvoiceConstants.MaxManualInterventionReasonLength} caracteres.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        Status = InvoiceStatus.RequiresManualIntervention;
        ManualInterventionReason = trimmed;
        ManualInterventionAtUtc = utcNow;

        RaiseDomainEvent(new InvoiceReconciliationExhaustedDomainEvent(
            Id,
            SaleId,
            TenantId,
            DocumentNumber,
            ReconciliationAttempts,
            ManualInterventionReason,
            utcNow));
    }
}
