using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Dashboard;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Dashboard;

/// <summary>
/// Proves the triage buckets (AD-11, FR-4) match what the seed actually contains, rather than just
/// asserting the endpoint returns 200 - a dashboard that answers the wrong question is worse than
/// one that fails to load, since nothing tells the user their triage is wrong.
/// </summary>
public sealed class DashboardTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task The_dashboard_buckets_match_the_seeded_demo_data()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var response = await client.GetAsync("/api/dashboard", ct);
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ct);
        dashboard.ShouldNotBeNull();

        // Submitted, waiting for a reviewer to start looking: the sample-storage request's
        // Crateworks quote and the near-expiry overflow-storage quote. The sample-storage request's
        // SecureBase quote is already UnderReview, not here.
        dashboard.QuotesNeedingReview.ShouldContain(q =>
            q.RequestTitle == "Regional sample storage — Southeast territory" && q.VendorOrganizationName == "Crateworks Packing & Crating");
        dashboard.QuotesNeedingReview.ShouldContain(q => q.RequestTitle == "Overflow inventory storage — holiday peak season");
        dashboard.QuotesNeedingReview.ShouldAllBe(q => q.Status == "Submitted");

        // Being actively reviewed: only the sample-storage request's SecureBase quote in the whole seed.
        dashboard.QuotesUnderReview.ShouldHaveSingleItem();
        dashboard.QuotesUnderReview[0].RequestTitle.ShouldBe("Regional sample storage — Southeast territory");
        dashboard.QuotesUnderReview[0].VendorOrganizationName.ShouldBe("SecureBase Self Storage");
        dashboard.QuotesUnderReview[0].PermittedActions.ShouldContain("Accept");

        // Expiring inside the 3-day window: only the overflow-storage quote (2 days out). The
        // sample-storage request's two quotes expire in 14 and 20 days and must not show up here
        // just because they are active.
        dashboard.QuotesExpiringSoon.ShouldHaveSingleItem();
        dashboard.QuotesExpiringSoon[0].RequestTitle.ShouldBe("Overflow inventory storage — holiday peak season");

        // Silence the invitation list exists to surface: sample storage has one invited partner
        // (Interstate) who never quoted, and the cold-chain pilot has three invited partners and
        // zero quotes at all. The trade show request also has invitations but every invitee
        // quoted, and it is Awarded besides.
        dashboard.RequestsAwaitingResponse.ShouldContain(r =>
            r.Title == "Regional sample storage — Southeast territory" && r.AwaitingVendorNames.ShouldHaveSingleItem() == "Interstate Freight Partners");
        var coldChainPilot = dashboard.RequestsAwaitingResponse.Single(r => r.Title == "Cold-chain sample storage pilot — new territory launch");
        coldChainPilot.AwaitingVendorNames.Count.ShouldBe(3);
        dashboard.RequestsAwaitingResponse.ShouldNotContain(r => r.Title == "Trade show fixture storage & drayage — West Coast expo season");
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
