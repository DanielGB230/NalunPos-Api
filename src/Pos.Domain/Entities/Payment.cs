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
public class Payment : AggregateRoot<Guid>, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
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
        Guid tenantId,
        Guid saleId,
        Money amount,
        PaymentMethod method,
        string? externalReference,
        TimeProvider timeProvider) : base(id)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        TenantId = TenantGuard.RequireTenantId(tenantId, "un pago");

        if (saleId == Guid.Empty)
        {
            throw new DomainException("El ID de la venta es requerido para registrar el pago.");
        }

        SaleId = saleId;
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
        Method = method;
        ExternalReference = externalReference?.Trim();
        Status = PaymentStatus.Pending;
        CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
    }

    public static Payment Create(
        Guid tenantId,
        Guid saleId,
        Money amount,
        PaymentMethod method,
        TimeProvider timeProvider)
    {
        return Create(tenantId, saleId, amount, method, null, timeProvider);
    }

    public static Payment Create(
        Guid tenantId,
        Guid saleId,
        Money amount,
        PaymentMethod method,
        string? externalReference,
        TimeProvider timeProvider)
    {
        return new Payment(Guid.NewGuid(), tenantId, saleId, amount, method, externalReference, timeProvider);
    }

    public void Process(TimeProvider timeProvider)
    {
        Process(null, timeProvider);
    }

    public void Process(string? externalVoucherReference, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status == PaymentStatus.Processed)
        {
            throw new DomainException("El pago ya fue procesado anteriormente.");
        }

        if (!string.IsNullOrWhiteSpace(externalVoucherReference))
        {
            ExternalReference = externalVoucherReference.Trim();
        }

        Status = PaymentStatus.Processed;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new PaymentProcessedDomainEvent(
            Id,
            SaleId,
            Amount.Amount,
            Amount.Currency,
            Method.ToString(),
            ExternalReference,
            utcNow));
    }

    public void Fail(string reason, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        Status = PaymentStatus.Failed;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        RaiseDomainEvent(new PaymentFailedDomainEvent(
            Id,
            SaleId,
            reason ?? "Error indeterminado en la pasarela de pagos.",
            utcNow));
    }
}
