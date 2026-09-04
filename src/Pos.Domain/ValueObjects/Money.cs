using Pos.Domain.Common;
using Pos.Domain.Exceptions;

namespace Pos.Domain.ValueObjects;

/// <summary>
/// Value Object para representar valores monetarios y su divisa.
/// Inmutable por diseño.
/// </summary>
public class Money : ValueObject
{
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "USD";

    // Constructor para EF Core Complex Types
    protected Money()
    {
    }

    private Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new InvalidMoneyException("El monto no puede ser negativo.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            throw new InvalidMoneyException("La divisa debe ser un código ISO de 3 caracteres (ej: USD, PEN).");
        }

        Amount = decimal.Round(amount, 4);
        Currency = currency.ToUpperInvariant();
    }

    public static Money Create(decimal amount, string currency = "USD")
    {
        return new Money(amount, currency);
    }

    public static Money Zero(string currency = "USD") => new(0, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidMoneyException($"No se pueden operar importes de distintas divisas ('{Currency}' vs '{other.Currency}').");
        }
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:F2} {Currency}";
}
