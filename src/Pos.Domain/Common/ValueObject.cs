namespace Pos.Domain.Common;

/// <summary>
/// Clase base para objetos de valor (Value Objects) en DDD.
/// Un Value Object carece de identidad propia y se define únicamente por el conjunto de sus componentes/atributos.
/// Es inmutable por diseño.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>
    /// Retorna los componentes atómicos que definen la igualdad del objeto de valor.
    /// </summary>
    protected abstract IEnumerable<object?> GetAtomicValues();

    public override bool Equals(object? obj)
    {
        return obj is ValueObject other && Equals(other);
    }

    public bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        return GetAtomicValues().SequenceEqual(other.GetAtomicValues());
    }

    public override int GetHashCode()
    {
        return GetAtomicValues()
            .Aggregate(0, (current, obj) => HashCode.Combine(current, obj?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}
