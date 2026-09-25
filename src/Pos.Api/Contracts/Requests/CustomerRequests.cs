namespace Pos.Api.Contracts.Requests;

public record GetCustomersRequest : PaginationRequest
{
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

public record CreateCustomerRequest(
    string FullName,
    string TaxId,
    string TaxCountryCode,
    string Email = "",
    string Phone = "",
    string? Street = null,
    string? City = null,
    string? ZipCode = null,
    string? Country = null
);

public record UpdateCustomerRequest(
    string FullName,
    string TaxId,
    string TaxCountryCode,
    string Email = "",
    string Phone = "",
    string? Street = null,
    string? City = null,
    string? ZipCode = null,
    string? Country = null
);
