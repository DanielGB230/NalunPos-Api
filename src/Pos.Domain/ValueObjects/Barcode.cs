using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Value Object para representar el Código de Barras (EAN-13, UPC, etc.) de un producto.
/// </summary>
public class Barcode : ValueObject
{
    public string Value { get; private set; } = string.Empty;

    protected Barcode()
    {
    }

    private Barcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("El código de barras no puede estar vacío.");
        }

        string trimmed = value.Trim();

        if (trimmed.Length < 5 || trimmed.Length > 50)
        {
            throw new DomainException("El código de barras debe contener entre 5 y 50 caracteres.");
        }

        Value = trimmed;
    }

    public static Barcode Create(string value) => new(value);

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
