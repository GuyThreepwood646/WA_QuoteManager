using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace QuoteManager.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores a <see cref="DateTimeOffset"/> as a fixed-width UTC ISO-8601 string.
/// </summary>
/// <remarks>
/// SQLite has no native date type, and EF Core's SQLite provider deliberately refuses to translate
/// ordering comparisons on <see cref="DateTimeOffset"/>: its default text form carries the offset,
/// so <c>2026-07-28 12:00:00+00:00</c> and <c>2026-07-28 08:00:00-04:00</c> are the same instant but
/// sort differently as text. Rather than lose every date filter in the application — "quotes
/// expiring soon" being the one the dashboard is built around — this normalises to UTC and writes a
/// fixed-width form in which lexicographic order and chronological order are the same thing. That
/// makes <c>&lt;</c> and <c>&gt;</c> translate to plain SQL and lets the index on
/// <c>Quotes(Status, ExpiresAt)</c> actually serve range scans.
///
/// Fixed width matters as much as UTC: without padded fractional seconds, "12:00:00.5" would sort
/// after "12:00:00.45" incorrectly. The trailing Z is a constant, so it does not disturb ordering.
///
/// The domain already treats every timestamp as UTC, so normalising here loses nothing. Round-trips
/// return an offset of zero, which is the truth about what was stored.
/// </remarks>
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
