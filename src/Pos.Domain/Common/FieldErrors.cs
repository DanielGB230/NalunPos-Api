namespace Pos.Domain.Common;

/// <summary>
/// Representa una colección inmutable de errores de validación agrupados por nombre de campo.
/// </summary>
public sealed record FieldErrors : IEquatable<FieldErrors>
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _errors;

    public FieldErrors(IDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        _errors = errors.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)(kvp.Value?.ToArray() ?? Array.Empty<string>()),
            StringComparer.Ordinal);
    }

    public FieldErrors(IReadOnlyDictionary<string, IReadOnlyList<string>> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        _errors = errors.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)(kvp.Value?.ToArray() ?? Array.Empty<string>()),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Diccionario de errores agrupados por campo.
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
