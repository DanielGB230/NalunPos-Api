using Pos.Domain.Entities;

namespace Pos.Application.Suppliers.DTOs;

public record SupplierDto(
    Guid Id,
    string Name,
    string ContactName,
    string Email,
    string Phone,
    string TaxId,
    string TaxCountryCode,
    string Street,
    string City,
    string ZipCode,
    string Country,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static SupplierDto FromEntity(Supplier supplier)
    {
        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.TaxId.Value,
            supplier.TaxId.CountryCode,
            supplier.Address.Street,
            supplier.Address.City,
            supplier.Address.ZipCode,
            supplier.Address.Country,
            supplier.IsActive,
            supplier.CreatedAtUtc,
            supplier.UpdatedAtUtc
        );
    }
}
