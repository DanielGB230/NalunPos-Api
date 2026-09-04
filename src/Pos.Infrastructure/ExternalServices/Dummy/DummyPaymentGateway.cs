using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;

namespace Pos.Infrastructure.ExternalServices.Dummy;

/// <summary>
/// Implementación simulada de la Capa Anti-Corrupción (ACL) para procesamiento de pagos.
/// Simula respuesta exitosa del procesador bancario/pasarela sin llamar servicios externos.
/// </summary>
public class DummyPaymentGateway : IPaymentGateway
{
    public Task<PaymentGatewayResult> ProcessPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payment);

        string mockTransactionId = $"TRX-DUMMY-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        var result = new PaymentGatewayResult(
            IsSuccess: true,
            TransactionId: mockTransactionId,
            ErrorMessage: null
        );

        return Task.FromResult(result);
    }
}
