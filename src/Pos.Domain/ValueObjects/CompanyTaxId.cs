using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Value Object para representar el RUC / identificador fiscal de un Tenant.
/// Inmutable por diseño.
/// </summary>
public class CompanyTaxId : ValueObject
{
    public string Value { get; private set; } = string.Empty;

    protected CompanyTaxId()
    {
    }

    public CompanyTaxId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("El identificador fiscal (TaxId) de la empresa no puede estar vacío.");
        }

        string trimmed = value.Trim().ToUpperInvariant();
        if (trimmed.Length < 4 || trimmed.Length > 25)
        {
            throw new DomainException("El identificador fiscal debe contener entre 4 y 25 caracteres.");
        }

        Value = trimmed;
    }

    public static CompanyTaxId Create(string value) => new(value);

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(CompanyTaxId taxId) => taxId?.Value ?? string.Empty;
}
