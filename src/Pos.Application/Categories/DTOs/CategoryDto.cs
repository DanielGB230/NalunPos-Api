using Pos.Domain.Entities;

namespace Pos.Application.Categories.DTOs;

public record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static CategoryDto FromEntity(Category category)
    {
        return new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.IsActive,
            category.CreatedAtUtc,
            category.UpdatedAtUtc
        );
    }
}
