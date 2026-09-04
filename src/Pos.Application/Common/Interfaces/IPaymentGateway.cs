using Pos.Domain.Entities;

namespace Pos.Application.Common.Interfaces;

public record PaymentGatewayResult(
    bool IsSuccess,
    string? TransactionId,
    string? ErrorMessage
);

/// <summary>
/// Contrato agnóstico de la Capa Anti-Corrupción (ACL) para pasarelas de pago externas.
/// No posee ninguna referencia a SDKs o librerías de terceros (Stripe, Paypal, MercadoPago, etc).
/// </summary>
public interface IPaymentGateway
{
    Task<PaymentGatewayResult> ProcessPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
}
