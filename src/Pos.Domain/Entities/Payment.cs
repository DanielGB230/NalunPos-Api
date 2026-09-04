using Pos.Domain.Common;
using Pos.Domain.DomainEvents;
using Pos.Domain.Enums;
using Pos.Domain.Exceptions;
using Pos.Domain.ValueObjects;

namespace Pos.Domain.Entities;

/// <summary>
/// Agregado para el Registro de Pago de una Venta.
/// CUMPLIMIENTO PCI-DSS (Sección 12 Prompt Maestro): Queda PROHIBIDO modelar PAN, CVV o fechas de caducidad.
/// </summary>
public class Payment : AggregateRoot<Guid>
{
    public Guid SaleId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentMethod Method { get; private set; }
    public string? ExternalReference { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Payment()
    {
    }

    private Payment(
        Guid id,
        Guid saleId,
        Money amount,
        PaymentMethod method,
        string? externalReference) : base(id)
    {
        if (saleId == Guid.Empty)
        {
            throw new DomainException("El ID de la venta es requerido para registrar el pago.");
        }

        SaleId = saleId;
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
        Method = method;
        ExternalReference = externalReference?.Trim();
        Status = PaymentStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static Payment Create(
        Guid saleId,
        Money amount,
        PaymentMethod method,
        string? externalReference = null)
    {
        return new Payment(Guid.NewGuid(), saleId, amount, method, externalReference);
    }

    public void Process(string? externalVoucherReference = null)
    {
        if (Status == PaymentStatus.Processed)
        {
            throw new DomainException("El pago ya fue procesado anteriormente.");
        }

        if (!string.IsNullOrWhiteSpace(externalVoucherReference))
        {
            ExternalReference = externalVoucherReference.Trim();
        }

        Status = PaymentStatus.Processed;

        RaiseDomainEvent(new PaymentProcessedDomainEvent(
            Id,
            SaleId,
            Amount.Amount,
            Amount.Currency,
            Method.ToString(),
            ExternalReference,
            DateTime.UtcNow));
    }

    public void Fail(string reason)
    {
        Status = PaymentStatus.Failed;

        RaiseDomainEvent(new PaymentFailedDomainEvent(
            Id,
            SaleId,
            reason ?? "Error indeterminado en la pasarela de pagos.",
            DateTime.UtcNow));
    }
}
