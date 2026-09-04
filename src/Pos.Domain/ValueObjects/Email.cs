using System.Text.RegularExpressions;
using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Value Object para representar y validar una dirección de correo electrónico.
/// Inmutable por diseño. Dos emails son iguales si su valor normalizado (lowercase) es igual.
/// </summary>
public class Email : ValueObject
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; private set; } = string.Empty;

    protected Email()
    {
    }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("El correo electrónico no puede estar vacío.");
        }

        string normalized = value.Trim().ToLowerInvariant();

        if (!EmailRegex.IsMatch(normalized))
        {
            throw new DomainException($"El formato del correo electrónico '{value}' no es válido.");
        }

        Value = normalized;
    }

    public static Email Create(string value) => new(value);

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email?.Value ?? string.Empty;
}
