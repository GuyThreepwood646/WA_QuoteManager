using System.Globalization;

namespace QuoteManager.Domain.Common;

/// <summary>
/// A monetary amount and its ISO-4217 currency, travelling together so a bare number can never
/// be compared or summed across currencies by accident.
/// </summary>
public readonly record struct Money
{
    public Money(decimal amount, string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A quoted amount must be greater than zero.");
        }

        if (currencyCode.Length != 3)
        {
            throw new ArgumentException("currency must be a written in ASCII letters.", nameof(currencyCode));
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        CurrencyCode = currencyCode.ToUpperInvariant();
    }

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {CurrencyCode}");
}
