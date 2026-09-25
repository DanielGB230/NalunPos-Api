namespace Pos.Api.Contracts.Requests;

public record CreateWarehouseRequest(
    Guid BranchId,
    string Name,
    string? Description,
    bool IsDefault
);
