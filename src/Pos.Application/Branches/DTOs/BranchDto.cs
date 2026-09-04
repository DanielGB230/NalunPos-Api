using Pos.Domain.Entities;

namespace Pos.Application.Branches.DTOs;

public record BranchDto(
    Guid Id,
    string Name,
    string Street,
    string City,
    string ZipCode,
    string Country,
    string PhoneNumber,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static BranchDto FromEntity(Branch branch)
    {
        return new BranchDto(
            branch.Id,
            branch.Name,
            branch.Address.Street,
            branch.Address.City,
            branch.Address.ZipCode,
            branch.Address.Country,
            branch.PhoneNumber,
            branch.IsActive,
            branch.CreatedAtUtc,
            branch.UpdatedAtUtc
        );
    }
}
