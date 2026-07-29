using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Requests;

/// <summary>
/// FR-4/FR-5: proves the activity timeline actually surfaces <c>AuditEntry</c> per request, and
/// applies AD-13's read-side filter exactly as <see cref="RequestsEndpointTests"/> proves for the
/// quotes/invitations lists themselves.
/// </summary>
public sealed class RequestActivityEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_admin_sees_the_full_history_of_a_request_with_a_completed_award()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestId = await FindRequestIdAsync("Trade show fixture storage & drayage — West Coast expo season", ct);
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync($"/api/requests/{requestId}/activity", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ActivityEntryResponse>>(ct);

        page.ShouldNotBeNull();

        // RequestCreated, two VendorInvited, two QuoteDrafted, two QuoteSubmitted, one
        // QuoteUnderReview, one QuoteAccepted, one QuoteRejected (the superseded sibling), and the
        // final RequestAwarded - every domain event this seed's award scenario raises.
        page.Items.ShouldContain(e => e.Action == "RequestCreated" && e.SubjectType == "Request");
        page.Items.Count(e => e.Action == "VendorInvited").ShouldBe(2);
        page.Items.ShouldContain(e => e.Action == "QuoteAccepted" && e.SubjectType == "Quote");
        page.Items.ShouldContain(e => e.Action == "QuoteRejected" && e.Summary.Contains("SupersededByAcceptedQuote"));
        page.Items.ShouldContain(e => e.Action == "RequestAwarded" && e.SubjectType == "Request");

        // Newest first: the award is the last thing that happened to this request.
        page.Items[0].Action.ShouldBe("RequestAwarded");
    }

    [Fact]
    public async Task A_vendor_viewing_a_shared_request_sees_only_its_own_quote_history()
    {
        var ct = TestContext.Current.CancellationToken;

        // Crateworks (vendor2@) and SecureBase both quoted on the sample-storage request.
        // Without the filter, Crateworks could read SecureBase's draft/submit/review history.
        var requestId = await FindRequestIdAsync("Regional sample storage — Southeast territory", ct);
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var response = await client.GetAsync($"/api/requests/{requestId}/activity?pageSize=100", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ActivityEntryResponse>>(ct);

        page.ShouldNotBeNull();

        // Request-level events remain visible - they name no vendor and carry no money.
        page.Items.ShouldContain(e => e.Action == "RequestCreated");
        page.Items.Count(e => e.Action == "VendorInvited").ShouldBe(3);

        // SecureBase's quote reached UnderReview; Crateworks must never see that transition, or
        // any other Quote-subject row, for a quote that is not its own.
        page.Items.ShouldNotContain(e => e.Action == "QuoteUnderReview");

        // Crateworks's own quote only ever reached Submitted in this seed.
        page.Items.ShouldContain(e => e.Action == "QuoteSubmitted");
    }

    [Fact]
    public async Task A_silent_invitee_vendor_sees_no_quote_subject_rows_at_all()
    {
        var ct = TestContext.Current.CancellationToken;

        // Interstate (vendor3@) was invited to the sample-storage request but never quoted.
        var requestId = await FindRequestIdAsync("Regional sample storage — Southeast territory", ct);
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");

        var response = await client.GetAsync($"/api/requests/{requestId}/activity?pageSize=100", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<ActivityEntryResponse>>(ct);

        page.ShouldNotBeNull();
        page.Items.ShouldNotContain(e => e.SubjectType == "Quote");
        page.Items.ShouldContain(e => e.Action == "RequestCreated");
    }

    [Fact]
    public async Task An_unknown_request_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync($"/api/requests/{Guid.NewGuid()}/activity", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<Guid> FindRequestIdAsync(string title, CancellationToken ct)
    {
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
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
