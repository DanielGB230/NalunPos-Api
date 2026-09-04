using Pos.Domain.Entities;
using Pos.Domain.Enums;

namespace Pos.Application.Inventory.DTOs;

public record InventoryMovementDto(
    Guid Id,
    Guid ProductId,
    decimal Quantity,
    InventoryMovementType MovementType,
    string MovementTypeName,
    Guid? ReferenceId,
    string? Notes,
    DateTime CreatedAtUtc
)
{
    public static InventoryMovementDto FromEntity(InventoryMovement movement)
    {
        return new InventoryMovementDto(
            movement.Id,
            movement.ProductId,
            movement.Quantity,
            movement.MovementType,
            movement.MovementType.ToString(),
            movement.ReferenceId,
            movement.Notes,
            movement.CreatedAtUtc
        );
    }
}
