using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace QuoteManager.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores a <see cref="DateTimeOffset"/> as a fixed-width UTC ISO-8601 string so lexicographic and
/// chronological order match — SQLite's provider otherwise refuses to translate ordering
/// comparisons on the type at all (AD-17).
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, string>
{
    private const string Format = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

    public UtcDateTimeOffsetConverter()
        : base(
            value => value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture),
            value => DateTimeOffset.ParseExact(
                value,
                Format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal))
    {
    }
}
