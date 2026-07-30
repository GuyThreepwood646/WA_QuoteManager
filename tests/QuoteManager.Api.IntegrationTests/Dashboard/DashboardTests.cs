using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Dashboard;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Domain.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Dashboard;

/// <summary>
/// Proves the dashboard groups by request rather than by quote (the bug this redesign fixes), that
/// vendor visibility is scoped the same way <c>GET /api/requests/{id}</c> already scopes it, and
/// that the KPI strip's numbers match the seed - rather than just asserting the endpoint returns
/// 200, which would say nothing about whether the triage is actually correct.
/// </summary>
public sealed class DashboardTests : IDisposable
{
    private const string SampleStorageTitle = "Regional sample storage — Southeast territory";
    private const string OverflowStorageTitle = "Overflow inventory storage — holiday peak season";
    private const string ColdChainPilotTitle = "Cold-chain sample storage pilot — new territory launch";
    private const string TradeShowTitle = "Trade show fixture storage & drayage — West Coast expo season";
    private const string PopUpStorageTitle = "Pop-up retail storage & fixture staging";
    private const string SeasonalLeaseTitle = "Seasonal storage lease — spring reset";

    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Reviewer_sees_one_grouped_card_per_request_with_full_visibility()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var response = await client.GetAsync("/api/dashboard", ct);
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ct);
        dashboard.ShouldNotBeNull();

        // The whole point of the redesign: one card, not three, for a request with quotes in
        // several different states.
        dashboard.Requests.Count(r => r.Title == SampleStorageTitle).ShouldBe(1);
        var sampleStorage = dashboard.Requests.Single(r => r.Title == SampleStorageTitle);
        sampleStorage.Quotes.ShouldContain(q => q.VendorOrganizationName == "Crateworks Packing & Crating" && q.Status == "Submitted");
        var secureBaseQuote = sampleStorage.Quotes.Single(q => q.VendorOrganizationName == "SecureBase Self Storage");
        secureBaseQuote.Status.ShouldBe("UnderReview");
        secureBaseQuote.PermittedActions.ShouldContain("Accept");
        sampleStorage.AwaitingVendorNames.ShouldHaveSingleItem().ShouldBe("Interstate Freight Partners");

        var overflowStorage = dashboard.Requests.Single(r => r.Title == OverflowStorageTitle);
        overflowStorage.Quotes.ShouldHaveSingleItem().IsExpiringSoon.ShouldBeTrue();
        overflowStorage.AwaitingVendorNames.ShouldBeEmpty();

        var coldChainPilot = dashboard.Requests.Single(r => r.Title == ColdChainPilotTitle);
        coldChainPilot.Quotes.ShouldBeEmpty();
        coldChainPilot.AwaitingVendorNames.Count.ShouldBe(3);

        // Awarded, so no longer needs triage.
        dashboard.Requests.ShouldNotContain(r => r.Title == TradeShowTitle);

        // Regression guards: an Open request whose only quote is Draft (not Submitted/UnderReview,
        // and the vendor has quoted so isn't silent either) must not appear just because it's Open.
        dashboard.Requests.ShouldNotContain(r => r.Title == PopUpStorageTitle);

        // Regression guard: an Open request whose quotes are both inactive (Expired/Withdrawn) and
        // whose invitees have both already quoted at some point must not appear either.
        dashboard.Requests.ShouldNotContain(r => r.Title == SeasonalLeaseTitle);

        // Nearest-deadline-first: overflow storage's quote expires in 2 days, the soonest deadline
        // of any qualifying request, so it sorts before sample storage (soonest active deadline in
        // 14 days) and the awaiting-only cold-chain pilot (no deadline at all, sorts last).
        dashboard.Requests.Select(r => r.Title).ShouldBe([OverflowStorageTitle, SampleStorageTitle, ColdChainPilotTitle]);
    }

    [Fact]
    public async Task Admin_sees_the_same_global_view_as_reviewer()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync("/api/dashboard", ct);
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ct);
        dashboard.ShouldNotBeNull();

        dashboard.Requests.Select(r => r.Title).ShouldBe([OverflowStorageTitle, SampleStorageTitle, ColdChainPilotTitle]);
        var sampleStorage = dashboard.Requests.Single(r => r.Title == SampleStorageTitle);
        sampleStorage.Quotes.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SecureBase_vendor_sees_only_their_own_quote_on_a_shared_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor@warehouseanywhere.test");

        var response = await client.GetAsync("/api/dashboard", ct);
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ct);
        dashboard.ShouldNotBeNull();

        var sampleStorage = dashboard.Requests.Single(r => r.Title == SampleStorageTitle);
        var onlyQuote = sampleStorage.Quotes.ShouldHaveSingleItem();
        onlyQuote.VendorOrganizationName.ShouldBe("SecureBase Self Storage");
        onlyQuote.Status.ShouldBe("UnderReview");
        sampleStorage.AwaitingVendorNames.ShouldBeEmpty();

        dashboard.Requests.Single(r => r.Title == OverflowStorageTitle).Quotes.ShouldHaveSingleItem();

        // Both invitees' quotes there are inactive (Expired/Withdrawn) - not qualifying, even for
        // the vendor whose own quote expired.
        dashboard.Requests.ShouldNotContain(r => r.Title == SeasonalLeaseTitle);
        dashboard.Requests.ShouldNotContain(r => r.Title == TradeShowTitle);
    }

    [Fact]
    public async Task Crateworks_vendor_sees_only_their_own_quote_on_the_shared_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var response = await client.GetAsync("/api/dashboard", ct);
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ct);
        dashboard.ShouldNotBeNull();

        var sampleStorage = dashboard.Requests.Single(r => r.Title == SampleStorageTitle);
        var onlyQuote = sampleStorage.Quotes.ShouldHaveSingleItem();
        onlyQuote.VendorOrganizationName.ShouldBe("Crateworks Packing & Crating");
        onlyQuote.Status.ShouldBe("Submitted");
    }

    [Fact]
    public async Task Interstate_vendor_sees_an_awaiting_only_card_with_no_quotes()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");

        var response = await client.GetAsync("/api/dashboard", ct);
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ct);
        dashboard.ShouldNotBeNull();

        var sampleStorage = dashboard.Requests.Single(r => r.Title == SampleStorageTitle);
        sampleStorage.Quotes.ShouldBeEmpty();
        // Scoping limits this vendor's own awaiting-list to their own org, so a vendor who hasn't
        // quoted yet sees their own name reflected back here - the frontend renders this as
        // "Awaiting your response" rather than the raw name, but the API shape is honest about it.
        sampleStorage.AwaitingVendorNames.ShouldHaveSingleItem().ShouldBe("Interstate Freight Partners");

        var coldChainPilot = dashboard.Requests.Single(r => r.Title == ColdChainPilotTitle);
        coldChainPilot.Quotes.ShouldBeEmpty();
        coldChainPilot.AwaitingVendorNames.ShouldHaveSingleItem().ShouldBe("Interstate Freight Partners");

        // Never invited to overflow storage.
        dashboard.Requests.ShouldNotContain(r => r.Title == OverflowStorageTitle);
    }

    [Fact]
    public async Task Kpis_reflect_seed_data_independent_of_role()
    {
        var ct = TestContext.Current.CancellationToken;

        var reviewerDashboard = await GetDashboardAsAsync("reviewer@warehouseanywhere.test", ct);
        // Every seeded request is Open except the one Awarded one (trade show).
        reviewerDashboard.Kpis.OpenRequestCount.ShouldBe(5);
        // Submitted/UnderReview quotes across the whole seed: sample storage's 2 (Crateworks
        // Submitted, SecureBase UnderReview) + overflow storage's 1 (SecureBase Submitted) - trade
        // show's Interstate quote is Accepted and its Crateworks quote was auto-rejected as a
        // sibling, pop-up storage's quote is Draft, seasonal lease's quotes are both inactive.
        reviewerDashboard.Kpis.QuotesAwaitingDecisionCount.ShouldBe(3);
        // 12 total invitations across all 6 requests; 8 have ever received a quote (any status
        // counts as "responded", including since-withdrawn/expired ones) - sample storage 2 of 3,
        // trade show 2 of 2, overflow storage 1 of 1, pop-up storage 1 of 1, cold-chain pilot 0 of
        // 3, seasonal lease 2 of 2.
        reviewerDashboard.Kpis.VendorResponseRatePercent.ShouldNotBeNull();
        reviewerDashboard.Kpis.VendorResponseRatePercent!.Value.ShouldBe(100.0 * 8 / 12, 0.01);

        var vendorDashboard = await GetDashboardAsAsync("vendor@warehouseanywhere.test", ct);
        // SecureBase's own Submitted/UnderReview quotes only: sample storage (UnderReview) +
        // overflow storage (Submitted).
        vendorDashboard.Kpis.QuotesAwaitingDecisionCount.ShouldBe(2);
        // Open request count is an unscoped, platform-wide number - same for every role.
        vendorDashboard.Kpis.OpenRequestCount.ShouldBe(5);
        // Vendor response rate is competitive/aggregate information about vendors in general, so a
        // pure Vendor viewer never receives it.
        vendorDashboard.Kpis.VendorResponseRatePercent.ShouldBeNull();
    }

    [Fact]
    public async Task Kpis_this_month_counts_match_an_independent_query()
    {
        var ct = TestContext.Current.CancellationToken;
        var dashboard = await GetDashboardAsAsync("reviewer@warehouseanywhere.test", ct);

        // Seed timestamps are relative to wall-clock "now" and can straddle a real calendar-month
        // boundary depending on when the suite happens to run, so the expected counts are computed
        // independently here (same current-UTC-month window the endpoint itself uses) rather than
        // hardcoded - this stays deterministic regardless of what day it is.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);

        var expectedOpened = await db.Requests.AsNoTracking()
            .CountAsync(r => r.CreatedAt >= monthStart && r.CreatedAt < monthEnd, ct);
        var expectedClosed = await db.AuditEntries.AsNoTracking()
            .CountAsync(a => a.SubjectType == nameof(Request)
                && (a.Action == nameof(RequestAwarded) || a.Action == nameof(RequestCancelled))
                && a.OccurredAt >= monthStart && a.OccurredAt < monthEnd, ct);

        dashboard.Kpis.RequestsOpenedThisMonth.ShouldBe(expectedOpened);
        dashboard.Kpis.RequestsClosedThisMonth.ShouldBe(expectedClosed);
    }

    private async Task<DashboardResponse> GetDashboardAsAsync(string email, CancellationToken cancellationToken)
    {
        var client = await LoginAsAsync(email);
        var response = await client.GetAsync("/api/dashboard", cancellationToken);
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(cancellationToken);
        dashboard.ShouldNotBeNull();
        return dashboard;
    }

    private async Task<HttpClient> LoginAsAsync(string email)
    {
        var client = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = DemoDataSeeder.DemoPassword }, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(ct);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }
}
