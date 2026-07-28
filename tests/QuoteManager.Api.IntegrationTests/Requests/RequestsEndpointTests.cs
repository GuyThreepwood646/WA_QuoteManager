using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Requests;

public sealed class RequestsEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Listing_requests_returns_every_seeded_request_with_a_quote_count()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@quotemgr.test");

        var response = await client.GetAsync("/api/requests", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RequestListItem>>(ct);

        page.ShouldNotBeNull();
        page.Total.ShouldBe(6);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(25);

        page.Items.ShouldContain(r => r.Title == "Car park resurfacing" && r.QuoteCount == 0);
        page.Items.ShouldContain(r => r.Title == "Replace rooftop HVAC units" && r.QuoteCount == 2);
    }

    [Fact]
    public async Task Requesting_a_page_size_over_the_cap_is_clamped_rather_than_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@quotemgr.test");

        var response = await client.GetAsync("/api/requests?pageSize=500", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RequestListItem>>(ct);

        page!.PageSize.ShouldBe(100);
    }

    [Fact]
    public async Task The_hvac_request_detail_shows_both_quotes_and_the_silent_invitee()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("reviewer@quotemgr.test");

        var requestId = await FindRequestIdAsync("Replace rooftop HVAC units", ct);

        var response = await client.GetAsync($"/api/requests/{requestId}", ct);
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);

        detail.ShouldNotBeNull();
        detail.ClientOrganizationName.ShouldBe("Northwind Facilities");
        detail.Quotes.Count.ShouldBe(2);
        detail.Invitations.Count.ShouldBe(3);

        // Two invitees quoted, the third (Ridgeline) never did - the exact signal AD-11 exists for.
        detail.Invitations.Count(i => i.HasQuoted).ShouldBe(2);
        detail.Invitations.ShouldContain(i => i.VendorOrganizationName == "Ridgeline Electrical" && !i.HasQuoted);

        // Both quotes are past Draft, so per AD-2's mutability rule the request itself is frozen.
        detail.IsEditable.ShouldBeFalse();

        var kestrelQuote = detail.Quotes.Single(q => q.VendorOrganizationName == "Kestrel HVAC");
        kestrelQuote.Status.ShouldBe("Submitted");
        kestrelQuote.PermittedActions.ShouldContain("StartReview");
    }

    [Fact]
    public async Task The_lobby_request_is_still_editable_because_its_only_quote_is_a_draft()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@quotemgr.test");

        var requestId = await FindRequestIdAsync("Lobby refurbishment", ct);

        var response = await client.GetAsync($"/api/requests/{requestId}", ct);
        var detail = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);

        detail!.IsEditable.ShouldBeTrue();
    }

    [Fact]
    public async Task An_unknown_request_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@quotemgr.test");

        var response = await client.GetAsync($"/api/requests/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<Guid> FindRequestIdAsync(string title, CancellationToken ct)
    {
        var client = await LoginAsAsync("admin@quotemgr.test");
        var response = await client.GetAsync("/api/requests?pageSize=100", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RequestListItem>>(ct);

        return page!.Items.Single(r => r.Title == title).Id;
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
