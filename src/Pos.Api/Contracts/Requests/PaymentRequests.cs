namespace Pos.Api.Contracts.Requests;

public record ProcessPaymentRequest(
    Guid SaleId,
    decimal Amount,
    Pos.Domain.Enums.PaymentMethod Method,
    string Currency = "USD",
    string? ExternalReference = null
);
