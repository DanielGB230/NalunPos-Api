namespace Pos.Api.Contracts.Requests;

public record CreateBranchRequest(
    string Name,
    string Street,
    string City,
    string Country,
    string ZipCode,
    string PhoneNumber
);

public record UpdateBranchRequest(
    string Name,
    string Street,
    string City,
    string Country,
    string ZipCode,
    string PhoneNumber
);
