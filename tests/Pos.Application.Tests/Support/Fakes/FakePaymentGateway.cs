namespace Pos.Application.Tests.Support.Fakes;

using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool ShouldSucceed { get; set; } = true;
    public Task<PaymentGatewayResult> ProcessPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        if (ShouldSucceed)
            return Task.FromResult(new PaymentGatewayResult(true, "TXN-99999", null));
        return Task.FromResult(new PaymentGatewayResult(false, null, "FONDOS_INSUFICIENTES"));
    }
}
