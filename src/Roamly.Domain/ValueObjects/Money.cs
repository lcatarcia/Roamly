namespace Roamly.Domain.ValueObjects;

/// <summary>
/// Importo con valuta esplicita. Complex type EF Core 10 (CONTEXT.md §2.1): mai un decimal nudo.
/// </summary>
public readonly record struct Money
{
    private const int IsoCurrencyLength = 3;

    /// <summary>Costruisce un importo validando il codice valuta ISO 4217.</summary>
    /// <param name="amount">Importo, 4 decimali sul database.</param>
    /// <param name="currency">Codice ISO 4217 di 3 lettere maiuscole.</param>
    public Money(decimal amount, string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (currency.Length != IsoCurrencyLength)
        {
            throw new ArgumentException("Il codice valuta ISO 4217 deve essere di 3 caratteri.", nameof(currency));
        }

        foreach (var character in currency)
        {
            if (!char.IsAsciiLetterUpper(character))
            {
                throw new ArgumentException(
                    "Il codice valuta ISO 4217 deve essere in lettere maiuscole ASCII.",
                    nameof(currency));
            }
        }

        Amount = amount;
        Currency = currency;
    }

    /// <summary>Importo, persistito come <c>decimal(19,4)</c>.</summary>
    public decimal Amount { get; }

    /// <summary>Codice ISO 4217, persistito come <c>char(3)</c>.</summary>
    public string Currency { get; }

    /// <summary>Somma due importi della stessa valuta.</summary>
    public static Money operator +(Money left, Money right) => left.Add(right);

    /// <summary>Sottrae due importi della stessa valuta.</summary>
    public static Money operator -(Money left, Money right) => left.Subtract(right);

    /// <summary>
    /// Somma. Lancia tra valute diverse: nessuna conversione implicita esiste in Roamly
    /// (CONTEXT.md §2.1, "si al dato, no alla conversione").
    /// </summary>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Sottrazione. Lancia tra valute diverse, per la stessa ragione di <see cref="Add"/>.</summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Operazione fra valute diverse: " + Currency + " e " + other.Currency + ".");
        }
    }
}
