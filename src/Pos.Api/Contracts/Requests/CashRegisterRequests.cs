namespace Pos.Api.Contracts.Requests;

public record CreateCashRegisterRequest(
    string Name,
    string SerialNumber = ""
);

public record OpenCashRegisterSessionRequest(
    Guid UserId,
    decimal InitialAmount,
    string Currency = "USD",
    string? Notes = null
);

public record CloseCashRegisterSessionRequest(
    decimal ActualFinalAmount,
    decimal ExpectedFinalAmount,
    string Currency = "USD",
    string? Notes = null
);
