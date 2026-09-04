using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Value Object inmutable para direcciones físicas.
/// </summary>
public class Address : ValueObject
{
    public string Street { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string ZipCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;

    protected Address()
    {
    }

    private Address(string street, string city, string zipCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            throw new DomainException("La calle de la dirección es requerida.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new DomainException("La ciudad de la dirección es requerida.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new DomainException("El país de la dirección es requerido.");
        }

        Street = street.Trim();
        City = city.Trim();
        ZipCode = zipCode?.Trim() ?? string.Empty;
        Country = country.Trim();
    }

    public static Address Create(string street, string city, string zipCode, string country)
    {
        return new Address(street, city, zipCode, country);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Street;
        yield return City;
        yield return ZipCode;
        yield return Country;
    }

    public override string ToString() => $"{Street}, {City}, {ZipCode}, {Country}";
}
