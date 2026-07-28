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
        var client = await LoginAsAsync("reviewer@quotemgr.test");

        var response = await client.GetAsync("/api/dashboard", ct);
        response.EnsureSuccessStatusCode();
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(ct);
        dashboard.ShouldNotBeNull();

        // Submitted, waiting for a reviewer to start looking: hvac's Kestrel quote and the
        // near-expiry generator quote. hvac's Bolt quote is already UnderReview, not here.
        dashboard.QuotesNeedingReview.ShouldContain(q => q.RequestTitle == "Replace rooftop HVAC units" && q.VendorOrganizationName == "Kestrel HVAC");
        dashboard.QuotesNeedingReview.ShouldContain(q => q.RequestTitle == "Emergency generator servicing");
        dashboard.QuotesNeedingReview.ShouldAllBe(q => q.Status == "Submitted");

        // Being actively reviewed: only hvac's Bolt quote in the whole seed.
        dashboard.QuotesUnderReview.ShouldHaveSingleItem();
        dashboard.QuotesUnderReview[0].RequestTitle.ShouldBe("Replace rooftop HVAC units");
        dashboard.QuotesUnderReview[0].VendorOrganizationName.ShouldBe("Bolt Mechanical");
        dashboard.QuotesUnderReview[0].PermittedActions.ShouldContain("Accept");

        // Expiring inside the 3-day window: only the generator quote (2 days out). hvac's two
        // quotes expire in 14 and 20 days and must not show up here just because they are active.
        dashboard.QuotesExpiringSoon.ShouldHaveSingleItem();
        dashboard.QuotesExpiringSoon[0].RequestTitle.ShouldBe("Emergency generator servicing");

        // Silence the invitation list exists to surface: hvac has one invited vendor (Ridgeline)
        // who never quoted, and car park has three invited vendors and zero quotes at all.
        // electrical also has invitations but every invitee quoted, and it is Awarded besides.
        dashboard.RequestsAwaitingResponse.ShouldContain(r =>
            r.Title == "Replace rooftop HVAC units" && r.AwaitingVendorNames.ShouldHaveSingleItem() == "Ridgeline Electrical");
        var carPark = dashboard.RequestsAwaitingResponse.Single(r => r.Title == "Car park resurfacing");
        carPark.AwaitingVendorNames.Count.ShouldBe(3);
        dashboard.RequestsAwaitingResponse.ShouldNotContain(r => r.Title == "Annual electrical safety inspection");
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
