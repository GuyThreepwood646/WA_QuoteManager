using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Organizations;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Persistence;

/// <summary>
/// Pins the on-disk timestamp encoding that date filtering depends on.
/// </summary>
/// <remarks>
/// SQLite compares these columns as text. If the encoding ever reverts to the provider default,
/// every range query keeps compiling and keeps returning plausible-looking wrong answers rather
/// than failing, so the format is asserted directly rather than trusted.
/// </remarks>
public sealed partial class TimestampStorageTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"qm-time-{Guid.NewGuid():N}.db");
    private QuoteManagerDbContext _context = null!;

    private string ConnectionString => $"Data Source={_databasePath}";

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$")]
    private static partial Regex SortableUtcPattern { get; }

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<QuoteManagerDbContext>()
            .UseSqlite(ConnectionString)
            .Options;

        _context = new QuoteManagerDbContext(options);
        await _context.Database.MigrateAsync(TestContext.Current.CancellationToken);
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
    public async Task Timestamps_are_written_in_fixed_width_utc_form()
    {
        var created = new DateTimeOffset(2026, 3, 9, 5, 4, 3, TimeSpan.FromHours(-7));
        _context.Organizations.Add(Organization.Create("Acme", OrganizationKind.Client, DomainActor.System, created));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var stored = await ScalarAsync("SELECT \"CreatedAt\" FROM \"Organizations\" LIMIT 1;");

        SortableUtcPattern.IsMatch(stored).ShouldBeTrue($"'{stored}' is not the sortable UTC form");

        // The offset is normalised away rather than preserved, so text order is instant order.
        stored.ShouldBe("2026-03-09T12:04:03.0000000Z");
    }

    [Fact]
    public async Task Text_ordering_in_the_database_agrees_with_chronological_ordering()
    {
        // Chosen so naive text ordering gets it backwards. Stored verbatim these read
        // "2026-05-02 02:00+00:00" and "2026-05-01 20:00-07:00", so the later instant sorts first
        // on text while being an hour after the other in real time.
        var earlier = new DateTimeOffset(2026, 5, 2, 2, 0, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 5, 1, 20, 0, 0, TimeSpan.FromHours(-7));

        earlier.UtcTicks.ShouldBeLessThan(later.UtcTicks, "the fixture must be a genuine inversion");
        later.Date.ShouldBeLessThan(earlier.Date, "and disagree on the date as written");

        _context.Organizations.Add(Organization.Create("Earlier", OrganizationKind.Client, DomainActor.System, earlier));
        _context.Organizations.Add(Organization.Create("Later", OrganizationKind.Client, DomainActor.System, later));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var firstByText = await ScalarAsync(
            "SELECT \"Name\" FROM \"Organizations\" ORDER BY \"CreatedAt\" ASC LIMIT 1;");

        firstByText.ShouldBe("Earlier");
    }

    [Fact]
    public async Task Range_filters_on_timestamps_translate_to_sql()
    {
        // The regression this guards: the SQLite provider refuses to translate comparisons on the
        // default DateTimeOffset mapping, which would break the dashboard's expiring-soon query.
        var now = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        _context.Organizations.Add(Organization.Create("Old", OrganizationKind.Client, DomainActor.System, now.AddDays(-5)));
        _context.Organizations.Add(Organization.Create("New", OrganizationKind.Client, DomainActor.System, now.AddDays(5)));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = _context.Organizations.AsNoTracking()
            .Where(organization => organization.CreatedAt < now)
            .Select(organization => organization.Name);

        // Fails to even produce SQL if the comparison is client-evaluated or untranslatable.
        query.ToQueryString().ShouldContain("WHERE");

        var names = await query.ToListAsync(TestContext.Current.CancellationToken);
        names.ShouldBe(["Old"]);
    }

    private async Task<string> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return (string)value!;
    }
}
