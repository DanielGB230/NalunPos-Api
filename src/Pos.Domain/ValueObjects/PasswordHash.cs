using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Value Object que envuelve un hash de contraseña ya calculado.
/// El Dominio NUNCA calcula el hash (eso es responsabilidad de Infrastructure).
/// Garantiza únicamente que el valor no esté vacío.
/// </summary>
public class PasswordHash : ValueObject
{
    public string Value { get; private set; } = string.Empty;

    protected PasswordHash()
    {
    }

    public PasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("El hash de la contraseña no puede estar vacío.");
        }

        Value = value;
    }

    public static PasswordHash Create(string value) => new(value);

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(PasswordHash hash) => hash?.Value ?? string.Empty;
}
