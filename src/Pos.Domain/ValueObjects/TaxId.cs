using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Value Object para el número de identificación tributaria (RUC, NIT, RFC, CIF).
/// Inmutable por diseño.
/// </summary>
public class TaxId : ValueObject
{
    public string Value { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = "PE";

    protected TaxId()
    {
    }

    private TaxId(string value, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("El número de identificación fiscal (TaxId) no puede estar vacío.");
        }

        string trimmedValue = value.Trim().ToUpperInvariant();
        if (trimmedValue.Length < 4 || trimmedValue.Length > 25)
        {
            throw new DomainException("El TaxId debe contener entre 4 y 25 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            throw new DomainException("El código de país del TaxId debe ser de 2 caracteres ISO (ej: PE, MX, CO).");
        }

        Value = trimmedValue;
        CountryCode = countryCode.ToUpperInvariant();
    }

    public static TaxId Create(string value, string countryCode = "PE")
    {
        return new TaxId(value, countryCode);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
        yield return CountryCode;
    }

    public override string ToString() => $"{Value} ({CountryCode})";
}
