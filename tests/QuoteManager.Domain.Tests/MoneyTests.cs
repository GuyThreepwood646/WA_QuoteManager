using QuoteManager.Domain.Common;

namespace QuoteManager.Domain.Tests;

/// <summary>
/// Defense in depth (per the architecture's stated principle): the API's <c>CreateQuoteRequest</c>
/// rejects a non-positive amount for the caller's benefit (a named, 400-worthy field error), but
/// <see cref="Money"/> itself is the actual invariant boundary every caller shares - a $0.00
/// storage/packing/freight quote is never a real offer, and nothing that constructs
/// <see cref="Money"/> directly, now or in the future, should be able to bypass that.
/// </summary>
public sealed class MoneyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_amount_is_rejected(decimal amount)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Money(amount, "USD"));
    }

    [Fact]
    public void A_positive_amount_rounds_to_two_decimal_places()
    {
        var money = new Money(19.999m, "usd");

        money.Amount.ShouldBe(20.00m);
        money.CurrencyCode.ShouldBe("USD");
    }
}
