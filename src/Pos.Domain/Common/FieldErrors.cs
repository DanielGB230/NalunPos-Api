using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Pos.Domain.Common;

/// <summary>
/// Representa una colección realmente inmutable de errores de validación agrupados por campo.
/// Almacena internamente un FrozenDictionary de ImmutableArray de cadenas.
/// Expone la referencia interna sin copias en lectura ni exposición de tipos mutables.
/// </summary>
public sealed record FieldErrors : IEquatable<FieldErrors>
{
    private readonly FrozenDictionary<string, IReadOnlyList<string>> _errors;

    /// <summary>
    /// Construye un <see cref="FieldErrors"/> a partir de un diccionario de campo → mensajes.
    /// Los arrays nulos se tratan como vacíos. Realiza una única copia defensiva a ImmutableArray al construir.
    /// </summary>
    /// <exception cref="ArgumentNullException">Si <paramref name="errors"/> es null.</exception>
    public FieldErrors(IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var copy = new Dictionary<string, IReadOnlyList<string>>(errors.Count, StringComparer.Ordinal);
        foreach (var (key, value) in errors)
        {
            copy[key] = value?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
        }

        _errors = copy.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Vista de solo lectura de los errores por campo.
    /// Devuelve la referencia interna sin copias en lectura.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Values => _errors;

    public bool Equals(FieldErrors? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_errors.Count != other._errors.Count) return false;

        foreach (var (key, value) in _errors)
        {
            if (!other._errors.TryGetValue(key, out var otherValue))
                return false;

            if (!value.SequenceEqual(otherValue, StringComparer.Ordinal))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var key in _errors.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            hash.Add(key, StringComparer.Ordinal);
            foreach (var val in _errors[key])
            {
                hash.Add(val, StringComparer.Ordinal);
            }
        }
        return hash.ToHashCode();
    }
}
