using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuoteManager.Domain.Quotes;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Persistence;

/// <summary>
/// Proves the foreign keys and the status lookup are enforced rather than merely declared.
/// </summary>
/// <remarks>
/// SQLite only honours foreign keys when <c>PRAGMA foreign_keys</c> is on, and it is off in the
/// engine's own default. A schema full of constraints that the connection never enforces looks
/// completely correct in a migration file and in a database browser, so this is verified rather
/// than assumed.
/// </remarks>
public sealed class ReferentialIntegrityTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"qm-fk-{Guid.NewGuid():N}.db");
    private QuoteManagerDbContext _context = null!;
    private Guid _clientOrganizationId;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<QuoteManagerDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        _context = new QuoteManagerDbContext(options);
        await _context.Database.MigrateAsync();

        _clientOrganizationId = Guid.CreateVersion7();
        await ExecuteAsync($"""
            INSERT INTO "Organizations" ("Id", "Name", "Kind", "CreatedAt", "Version")
            VALUES ({Quoted(_clientOrganizationId)}, 'Acme', 'Client', '2026-07-28T12:00:00.0000000Z', 1);
            """);
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
    public async Task Foreign_key_enforcement_is_actually_switched_on()
    {
        var pragma = await ScalarAsync("PRAGMA foreign_keys;");

        pragma.ShouldBe("1", "every constraint below is decoration if this is off");
    }

    [Fact]
    public async Task A_request_cannot_reference_an_organisation_that_does_not_exist()
    {
        var exception = await Should.ThrowAsync<SqliteException>(() => ExecuteAsync($"""
            INSERT INTO "Requests" ("Id", "Title", "Description", "ClientOrganizationId", "Status",
                                    "NeededBy", "CreatedAt", "Version")
            VALUES ({Quoted(Guid.CreateVersion7())}, 'Orphan', NULL, {Quoted(Guid.CreateVersion7())},
                    'Open', NULL, '2026-07-28T12:00:00.0000000Z', 1);
            """));

        exception.Message.ShouldContain("FOREIGN KEY");
    }

    [Fact]
    public async Task A_quote_cannot_reference_a_vendor_that_does_not_exist()
    {
        var requestId = await InsertRequestAsync();

        var exception = await Should.ThrowAsync<SqliteException>(() =>
            InsertQuoteAsync(requestId, Guid.CreateVersion7(), nameof(QuoteStatus.Draft)));

        exception.Message.ShouldContain("FOREIGN KEY");
    }

    [Fact]
    public async Task A_quote_cannot_hold_a_status_outside_the_lookup_table()
    {
        var requestId = await InsertRequestAsync();
        var vendorId = await InsertVendorAsync();

        // The domain enum makes this unreachable through the aggregate. The point of the lookup
        // table is that it is unreachable through the database either, whatever writes to it.
        var exception = await Should.ThrowAsync<SqliteException>(() =>
            InsertQuoteAsync(requestId, vendorId, "Renegotiating"));

        exception.Message.ShouldContain("FOREIGN KEY");
    }

    [Fact]
    public async Task The_status_lookup_matches_the_domain_enum_exactly()
    {
        // Two lists that can drift: add a status to the enum and forget the migration, and the
        // database silently refuses to store the new state at runtime.
        var stored = await _context.QuoteStatuses.AsNoTracking()
            .OrderBy(s => s.DisplayOrder)
            .Select(s => s.Status)
            .ToListAsync(TestContext.Current.CancellationToken);

        stored.ShouldBe(Enum.GetValues<QuoteStatus>());
    }

    [Fact]
    public async Task The_lookup_agrees_with_the_domain_about_which_states_are_terminal()
    {
        var stored = await _context.QuoteStatuses.AsNoTracking()
            .ToDictionaryAsync(s => s.Status, s => s.IsTerminal, TestContext.Current.CancellationToken);

        foreach (var status in Enum.GetValues<QuoteStatus>())
        {
            stored[status].ShouldBe(QuoteTransitions.IsTerminal(status), $"{status} disagrees");
        }
    }

    [Fact]
    public async Task The_same_vendor_cannot_be_invited_to_one_request_twice()
    {
        var requestId = await InsertRequestAsync();
        var vendorId = await InsertVendorAsync();

        await InviteAsync(requestId, vendorId);

        var exception = await Should.ThrowAsync<SqliteException>(() => InviteAsync(requestId, vendorId));

        exception.Message.ShouldContain("UNIQUE");
    }

    [Fact]
    public async Task An_organisation_still_referenced_by_a_request_cannot_be_deleted()
    {
        await InsertRequestAsync();

        // Restrict rather than cascade: silently destroying a request and its whole audit history
        // because someone tidied up an organisation would be far worse than a failed delete.
        var exception = await Should.ThrowAsync<SqliteException>(() =>
            ExecuteAsync($"""DELETE FROM "Organizations" WHERE "Id" = {Quoted(_clientOrganizationId)};"""));

        exception.Message.ShouldContain("FOREIGN KEY");
    }

    private async Task<Guid> InsertRequestAsync()
    {
        var requestId = Guid.CreateVersion7();
        await ExecuteAsync($"""
            INSERT INTO "Requests" ("Id", "Title", "Description", "ClientOrganizationId", "Status",
                                    "NeededBy", "CreatedAt", "Version")
            VALUES ({Quoted(requestId)}, 'Replace the HVAC units', NULL, {Quoted(_clientOrganizationId)},
                    'Open', NULL, '2026-07-28T12:00:00.0000000Z', 1);
            """);
        return requestId;
    }

    private async Task<Guid> InsertVendorAsync()
    {
        var vendorId = Guid.CreateVersion7();
        await ExecuteAsync($"""
            INSERT INTO "Organizations" ("Id", "Name", "Kind", "CreatedAt", "Version")
            VALUES ({Quoted(vendorId)}, 'Vendor {vendorId:N}', 'Vendor', '2026-07-28T12:00:00.0000000Z', 1);
            """);
        return vendorId;
    }

    private Task InsertQuoteAsync(Guid requestId, Guid vendorId, string status) =>
        ExecuteAsync($"""
            INSERT INTO "Quotes" ("Id", "RequestId", "VendorOrganizationId", "Status", "ExpiresAt",
                                  "Notes", "CreatedAt", "StatusChangedAt", "StatusReason",
                                  "AmountMinorUnits", "CurrencyCode", "Version")
            VALUES ({Quoted(Guid.CreateVersion7())}, {Quoted(requestId)}, {Quoted(vendorId)},
                    '{status}', NULL, NULL, '2026-07-28T12:00:00.0000000Z', '2026-07-28T12:00:00.0000000Z',
                    NULL, 100000, 'USD', 1);
            """);

    private Task InviteAsync(Guid requestId, Guid vendorId) =>
        ExecuteAsync($"""
            INSERT INTO "RequestInvitations" ("RequestId", "VendorOrganizationId", "InvitedAt")
            VALUES ({Quoted(requestId)}, {Quoted(vendorId)}, '2026-07-28T12:00:00.0000000Z');
            """);

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<string?> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))?.ToString();
    }

    private static string Quoted(Guid id) => $"'{id.ToString().ToUpperInvariant()}'";
}
