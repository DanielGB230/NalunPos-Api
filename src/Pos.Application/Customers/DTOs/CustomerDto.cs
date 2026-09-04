using Pos.Domain.Entities;

namespace Pos.Application.Customers.DTOs;

public record CustomerDto(
    Guid Id,
    string FullName,
    string Email,
    string Phone,
    string TaxId,
    string TaxCountryCode,
    string? Street,
    string? City,
    string? ZipCode,
    string? Country,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
)
{
    public static CustomerDto FromEntity(Customer customer)
    {
        return new CustomerDto(
            customer.Id,
            customer.FullName,
            customer.Email,
            customer.Phone,
            customer.TaxId.Value,
            customer.TaxId.CountryCode,
            customer.Address?.Street,
            customer.Address?.City,
            customer.Address?.ZipCode,
            customer.Address?.Country,
            customer.IsActive,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc
        );
    }
}
