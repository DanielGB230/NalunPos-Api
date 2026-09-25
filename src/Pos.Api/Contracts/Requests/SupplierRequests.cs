namespace Pos.Api.Contracts.Requests;

public record GetSuppliersRequest : PaginationRequest
{
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
}

public record CreateSupplierRequest(
    string Name,
    string ContactName,
    string Email,
    string Phone,
    string Street,
    string City,
    string Country,
    string ZipCode,
    string TaxIdValue,
    string TaxCountryCode
);

public record UpdateSupplierRequest(
    string Name,
    string ContactName,
    string Email,
    string Phone,
    string Street,
    string City,
    string Country,
    string ZipCode,
    string TaxIdValue,
    string TaxCountryCode
);
