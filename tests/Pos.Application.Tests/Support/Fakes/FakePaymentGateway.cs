namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;

public sealed class FakePaymentGateway : IPaymentGateway
{
    private PaymentGatewayResult? _result;

    /// <summary>Configura la pasarela para aprobar el pago con un identificador de transacción.</summary>
    public void Approve(string transactionId = "TXN-99999")
    {
        _result = new PaymentGatewayResult(true, transactionId, null);
    }

    /// <summary>Configura la pasarela para rechazar el pago con el motivo indicado.</summary>
    public void Decline(string reason = "FONDOS_INSUFICIENTES")
    {
        _result = new PaymentGatewayResult(false, null, reason);
    }

    public Task<PaymentGatewayResult> ProcessPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (_result is null)
        {
            throw new InvalidOperationException("FakePaymentGateway no configurado: llama Approve() o Decline()");
        }

        return Task.FromResult(_result);
    }
}
