using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Persistence;

/// <summary>
/// Checks that the demo seed produces a database worth demonstrating.
/// </summary>
/// <remarks>
/// The seed is not test fixture data; it is what a reviewer sees within seconds of a fresh clone.
/// If it misses a lifecycle state, a screen has nothing to show. If it is not idempotent, a second
/// start-up either duplicates everything or crashes on a constraint.
/// </remarks>
public sealed class DemoSeedTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"qm-seed-{Guid.NewGuid():N}.db");
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));
    private QuoteManagerDbContext _context = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<QuoteManagerDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        _context = new QuoteManagerDbContext(options);
        await _context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await NewSeeder().SeedAsync(TestContext.Current.CancellationToken);
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
    public async Task Running_the_seeder_twice_changes_nothing()
    {
        var before = await CountsAsync();

        await NewSeeder().SeedAsync(TestContext.Current.CancellationToken);

        var after = await CountsAsync();
        after.ShouldBe(before);
    }

    [Fact]
    public async Task Every_quote_lifecycle_state_is_represented()
    {
        // A state absent here is a state no screen can demonstrate, and the transition table's
        // handling of it goes unexercised in the demo.
        var present = await _context.Quotes.AsNoTracking()
            .Select(q => q.Status)
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        present.ShouldBe(Enum.GetValues<QuoteStatus>(), ignoreOrder: true);
    }

    [Fact]
    public async Task There_is_an_account_for_every_role()
    {
        var roles = await _context.Users.AsNoTracking()
            .Select(u => u.Roles)
            .ToListAsync(TestContext.Current.CancellationToken);

        foreach (var role in new[] { AppRole.Admin, AppRole.Requester, AppRole.Reviewer, AppRole.Vendor })
        {
            roles.ShouldContain(role, $"no seeded account can act as {role}");
        }
    }

    [Fact]
    public async Task The_documented_demo_password_actually_works()
    {
        // The README publishes this password. If hashing and verification disagree, the demo stops
        // at the login screen.
        var user = await _context.Users.AsNoTracking()
            .SingleAsync(u => u.Email == "admin@warehouseanywhere.test", TestContext.Current.CancellationToken);

        var result = new PasswordHasher<AppUser>()
            .VerifyHashedPassword(user, user.PasswordHash, DemoDataSeeder.DemoPassword);

        result.ShouldBe(PasswordVerificationResult.Success);
    }

    [Fact]
    public async Task Passwords_are_hashed_rather_than_stored()
    {
        var hashes = await _context.Users.AsNoTracking()
            .Select(u => u.PasswordHash)
            .ToListAsync(TestContext.Current.CancellationToken);

        hashes.ShouldAllBe(hash => hash != DemoDataSeeder.DemoPassword && hash.Length > 40);
    }

    [Fact]
    public async Task Exactly_one_request_is_awarded_and_it_has_exactly_one_accepted_quote()
    {
        var awarded = await _context.Requests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Awarded)
            .ToListAsync(TestContext.Current.CancellationToken);

        awarded.Count.ShouldBe(1, "the demo needs a completed award to show the invariant in action");
        awarded[0].Quotes.Count(q => q.Status == QuoteStatus.Accepted).ShouldBe(1);

        // The losing quote was superseded automatically rather than left dangling.
        awarded[0].Quotes.ShouldContain(q =>
            q.Status == QuoteStatus.Rejected && q.StatusReason == "SupersededByAcceptedQuote");
    }

    [Fact]
    public async Task At_least_one_quote_is_close_to_expiry_so_the_dashboard_has_something_urgent()
    {
        var now = _clock.GetUtcNow();

        var expiringSoon = await _context.Quotes.AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Submitted
                        && q.ExpiresAt != null
                        && q.ExpiresAt < now.AddDays(3))
            .CountAsync(TestContext.Current.CancellationToken);

        expiringSoon.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task At_least_one_request_has_invited_vendors_who_never_responded()
    {
        var requests = await _context.Requests.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);

        // The case the invitation list exists for: silence that would otherwise be invisible.
        requests.ShouldContain(r => r.Invitations.Count > 0 && r.Quotes.Count == 0);
        requests.ShouldContain(r => r.AwaitingResponseFrom.Count > 0 && r.Quotes.Count > 0);
    }

    [Fact]
    public async Task Every_quote_belongs_to_a_vendor_organization_that_exists()
    {
        var vendorIds = await _context.Organizations.AsNoTracking()
            .Select(o => o.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var quoteVendorIds = await _context.Quotes.AsNoTracking()
            .Select(q => q.VendorOrganizationId)
            .Distinct()
            .ToListAsync(TestContext.Current.CancellationToken);

        quoteVendorIds.ShouldAllBe(id => vendorIds.Contains(id));
    }

    private DemoDataSeeder NewSeeder() =>
        new(_context, new PasswordHasher<AppUser>(), _clock, NullLogger<DemoDataSeeder>.Instance);

    private async Task<(int Organizations, int Users, int Requests, int Quotes, int Invitations)> CountsAsync() =>
        (await _context.Organizations.CountAsync(TestContext.Current.CancellationToken),
         await _context.Users.CountAsync(TestContext.Current.CancellationToken),
         await _context.Requests.CountAsync(TestContext.Current.CancellationToken),
         await _context.Quotes.CountAsync(TestContext.Current.CancellationToken),
         await _context.RequestInvitations.CountAsync(TestContext.Current.CancellationToken));
}
