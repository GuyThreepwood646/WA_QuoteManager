using Microsoft.Extensions.Logging;

namespace QuoteManager.Infrastructure.Persistence;

/// <summary>
/// Source-generated log messages for start-up and seeding.
/// </summary>
/// <remarks>
/// The <c>LoggerMessage</c> generator produces strongly typed, pre-compiled delegates with the
/// enabled-check built in, so an argument is never formatted or boxed for a log level that is
/// switched off. Written as static methods taking the logger explicitly because the generator
/// cannot see a logger captured as a primary-constructor parameter.
/// </remarks>
internal static partial class PersistenceLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Applying {Count} pending migration(s): {Migrations}")]
    public static partial void ApplyingMigrations(ILogger logger, int count, string migrations);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Schema is up to date; no migrations pending")]
    public static partial void SchemaUpToDate(ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Production environment detected; skipping demo seed")]
    public static partial void SkippingSeedInProduction(ILogger logger);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Database already contains data; skipping demo seed")]
    public static partial void SeedSkippedDataPresent(ILogger logger);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Seeded {Organizations} organizations, {Users} users, {Requests} requests and {Quotes} quotes")]
    public static partial void SeedCompleted(
        ILogger logger,
        int organizations,
        int users,
        int requests,
        int quotes);
}
