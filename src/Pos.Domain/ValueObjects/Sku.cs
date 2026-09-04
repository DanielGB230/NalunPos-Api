using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Value Object para representar el Stock Keeping Unit (SKU) del producto.
/// </summary>
public class Sku : ValueObject
{
    public string Value { get; private set; } = string.Empty;

    protected Sku()
    {
    }

    private Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidSkuException("El SKU no puede estar vacío.");
        }

        string trimmed = value.Trim().ToUpperInvariant();

        if (trimmed.Length < 3 || trimmed.Length > 30)
        {
            throw new InvalidSkuException("El SKU debe tener entre 3 y 30 caracteres.");
        }

        Value = trimmed;
    }

    public static Sku Create(string value) => new(value);

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
