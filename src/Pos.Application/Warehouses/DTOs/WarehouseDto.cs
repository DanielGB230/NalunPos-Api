using Pos.Domain.Entities;

namespace Pos.Application.Warehouses.DTOs;

public record WarehouseDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive,
    DateTime CreatedAtUtc
)
{
    public static WarehouseDto FromEntity(Warehouse w) =>
        new(w.Id, w.BranchId, w.Name, w.Description, w.IsDefault, w.IsActive, w.CreatedAtUtc);
}
