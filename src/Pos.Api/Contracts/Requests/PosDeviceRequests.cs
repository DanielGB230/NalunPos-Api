namespace Pos.Api.Contracts.Requests;

public record RegisterPosDeviceRequest(
    Guid BranchId,
    string Name,
    string SerialNumber
);
