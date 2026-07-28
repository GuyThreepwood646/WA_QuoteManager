using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuoteManager.Domain.Quotes;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Persistence;

/// <summary>
/// Proves the database half of AD-3 actually refuses a second accepted quote.
/// </summary>
/// <remarks>
/// Every insert here is raw SQL, deliberately. Going through the aggregate would prove only that
/// the in-memory check works, which the domain tests already cover. The failure this guards
/// against is silent: map the status enum to EF's default ordinal and the index filter compares
/// against the literal <c>'Accepted'</c>, matches nothing forever, and the constraint quietly
/// stops existing while every other test stays green.
/// </remarks>
public sealed class AcceptedQuoteIndexTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"qm-index-{Guid.NewGuid():N}.db");
    private QuoteManagerDbContext _context = null!;
    private Guid _requestId;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<QuoteManagerDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        _context = new QuoteManagerDbContext(options);

        // MigrateAsync, never EnsureCreated: the filtered index exists in the migration, and
        // EnsureCreated would bypass migration history in a way that hides exactly this drift.
        await _context.Database.MigrateAsync();

        _requestId = Guid.CreateVersion7();
        var organizationId = Guid.CreateVersion7();

        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Organizations" ("Id", "Name", "Kind", "CreatedAt", "Version")
            VALUES ({0}, 'Acme', 'Client', '2026-07-28T12:00:00+00:00', 1);
            """.Replace("{0}", Quoted(organizationId)));

        await _context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Requests" ("Id", "Title", "Description", "ClientOrganizationId", "Status", "NeededBy", "CreatedAt", "Version")
            VALUES (@id, 'Replace the HVAC units', NULL, @org, 'Open', NULL, '2026-07-28T12:00:00+00:00', 1);
            """.Replace("@id", Quoted(_requestId)).Replace("@org", Quoted(organizationId)));
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task The_database_refuses_a_second_accepted_quote_on_the_same_request()
    {
        await InsertQuoteAsync(QuoteStatus.Accepted);

        var second = await Should.ThrowAsync<SqliteException>(() => InsertQuoteAsync(QuoteStatus.Accepted));

        second.SqliteErrorCode.ShouldBe(19, "SQLITE_CONSTRAINT");
        second.Message.ShouldContain("UNIQUE");
    }

    [Fact]
    public async Task The_index_constrains_only_accepted_rows()
    {
        // If the filter were wrong or absent, the index would be a plain unique constraint on
        // RequestId and this would fail on the second insert — which is the other way the
        // configuration can be broken.
        await InsertQuoteAsync(QuoteStatus.Submitted);
        await InsertQuoteAsync(QuoteStatus.Submitted);
        await InsertQuoteAsync(QuoteStatus.Rejected);
        await InsertQuoteAsync(QuoteStatus.Accepted);

        var quotes = await _context.Quotes.AsNoTracking()
            .Where(q => q.RequestId == _requestId)
            .ToListAsync(TestContext.Current.CancellationToken);

        quotes.Count.ShouldBe(4);
        quotes.Count(q => q.Status == QuoteStatus.Accepted).ShouldBe(1);
    }

    [Fact]
    public async Task Status_is_stored_as_text_so_the_index_filter_can_match_it()
    {
        await InsertQuoteAsync(QuoteStatus.Accepted);

        var storedType = await ScalarAsync($"SELECT typeof(\"Status\") FROM \"Quotes\" WHERE \"RequestId\" = {Quoted(_requestId)}");
        var storedValue = await ScalarAsync($"SELECT \"Status\" FROM \"Quotes\" WHERE \"RequestId\" = {Quoted(_requestId)}");

        storedType.ShouldBe("text");
        storedValue.ShouldBe("Accepted");
    }

    [Fact]
    public async Task Money_is_stored_as_integer_minor_units_so_amounts_sort_numerically()
    {
        await InsertQuoteAsync(QuoteStatus.Submitted, amountMinorUnits: 900_00);
        await InsertQuoteAsync(QuoteStatus.Submitted, amountMinorUnits: 1000_00);

        var ordered = await _context.Quotes.AsNoTracking()
            .Where(q => q.RequestId == _requestId)
            .OrderBy(q => q.Amount.Amount)
            .Select(q => q.Amount.Amount)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Stored as TEXT, SQLite would order these lexicographically and put 900 after 1000.
        ordered.ShouldBe([900m, 1000m]);
    }

    private async Task InsertQuoteAsync(QuoteStatus status, long amountMinorUnits = 1000_00)
    {
        var sql = $"""
            INSERT INTO "Quotes" ("Id", "RequestId", "VendorOrganizationId", "Status", "ExpiresAt",
                                  "Notes", "CreatedAt", "StatusChangedAt", "StatusReason",
                                  "AmountMinorUnits", "CurrencyCode", "Version")
            VALUES ({Quoted(Guid.CreateVersion7())}, {Quoted(_requestId)}, {Quoted(Guid.CreateVersion7())},
                    '{status}', NULL, NULL, '2026-07-28T12:00:00+00:00', '2026-07-28T12:00:00+00:00', NULL,
                    {amountMinorUnits}, 'USD', 1);
            """;

        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<string?> ScalarAsync(string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
    }

    private static string Quoted(Guid id) => $"'{id.ToString().ToUpperInvariant()}'";
}
