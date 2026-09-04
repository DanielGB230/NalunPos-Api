using Pos.Domain.Entities;

namespace Pos.Application.Payments.DTOs;

public record PaymentDto(
    Guid Id,
    Guid SaleId,
    decimal Amount,
    string Currency,
    string MethodName,
    string? ExternalReference,
    string StatusName,
    DateTime CreatedAtUtc
)
{
    public static PaymentDto FromEntity(Payment payment)
    {
        return new PaymentDto(
            payment.Id,
            payment.SaleId,
            payment.Amount.Amount,
            payment.Amount.Currency,
            payment.Method.ToString(),
            payment.ExternalReference,
            payment.Status.ToString(),
            payment.CreatedAtUtc
        );
    }
}
