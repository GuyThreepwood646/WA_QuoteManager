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
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync("/api/requests", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RequestListItem>>(ct);

        page.ShouldNotBeNull();
        page.Total.ShouldBe(6);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(25);

        page.Items.ShouldContain(r => r.Title == "Cold-chain sample storage pilot — new territory launch" && r.QuoteCount == 0);
        page.Items.ShouldContain(r => r.Title == "Regional sample storage — Southeast territory" && r.QuoteCount == 2);
    }

    [Fact]
    public async Task Requesting_a_page_size_over_the_cap_is_clamped_rather_than_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync("/api/requests?pageSize=500", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RequestListItem>>(ct);

        page!.PageSize.ShouldBe(100);
    }

    [Fact]
    public async Task Requesting_a_non_positive_page_or_page_size_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync("/api/requests?page=0&pageSize=-5", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest,
            "clamping only makes sense for a missing value or one that's too high - an explicit non-positive value has no sensible default");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Page");
        problem.Errors.ShouldContainKey("PageSize");
    }

    [Fact]
    public async Task The_sample_storage_request_detail_shows_both_quotes_and_the_silent_invitee()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var requestId = await FindRequestIdAsync("Regional sample storage — Southeast territory", ct);

        var response = await client.GetAsync($"/api/requests/{requestId}", ct);
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);

        detail.ShouldNotBeNull();
        detail.ClientOrganizationName.ShouldBe("Meridian Pharma Sampling");
        detail.Quotes.Count.ShouldBe(2);
        detail.Invitations.Count.ShouldBe(3);

        // Two invitees quoted, the third (Interstate) never did - the signal this list exists for.
        detail.Invitations.Count(i => i.HasQuoted).ShouldBe(2);
        detail.Invitations.ShouldContain(i => i.VendorOrganizationName == "Interstate Freight Partners" && !i.HasQuoted);

        // Both quotes are past Draft, so the request's own mutability rule freezes the request itself.
        detail.IsEditable.ShouldBeFalse();

        var crateworksQuote = detail.Quotes.Single(q => q.VendorOrganizationName == "Crateworks Packing & Crating");
        crateworksQuote.Status.ShouldBe("Submitted");
        crateworksQuote.PermittedActions.ShouldContain("StartReview");
    }

    [Fact]
    public async Task A_vendor_viewing_a_shared_request_sees_only_its_own_quote_and_invitation()
    {
        var ct = TestContext.Current.CancellationToken;

        // Crateworks (vendor2@) quoted on the sample-storage request alongside SecureBase, with
        // Interstate invited but silent. Without the read-side vendor filter, Crateworks could
        // read SecureBase's amount, notes, and status straight off this response.
        var requestId = await FindRequestIdAsync("Regional sample storage — Southeast territory", ct);
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var response = await client.GetAsync($"/api/requests/{requestId}", ct);
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);

        detail.ShouldNotBeNull();
        detail.Quotes.Count.ShouldBe(1, "a Vendor must not see a competitor's quote on a shared request");
        detail.Quotes.Single().VendorOrganizationName.ShouldBe("Crateworks Packing & Crating");

        detail.Invitations.Count.ShouldBe(1, "a Vendor must not see who else was invited to quote");
        detail.Invitations.Single().VendorOrganizationName.ShouldBe("Crateworks Packing & Crating");
        detail.Invitations.Single().HasQuoted.ShouldBeTrue();
    }

    [Fact]
    public async Task A_silent_invitee_vendor_sees_its_own_invitation_and_no_quotes_at_all()
    {
        var ct = TestContext.Current.CancellationToken;

        // Interstate (vendor3@) was invited to the sample-storage request but never quoted.
        var requestId = await FindRequestIdAsync("Regional sample storage — Southeast territory", ct);
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");

        var response = await client.GetAsync($"/api/requests/{requestId}", ct);
        response.EnsureSuccessStatusCode();
        var detail = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);

        detail.ShouldNotBeNull();
        detail.Quotes.ShouldBeEmpty();
        detail.Invitations.Count.ShouldBe(1);
        detail.Invitations.Single().VendorOrganizationName.ShouldBe("Interstate Freight Partners");
        detail.Invitations.Single().HasQuoted.ShouldBeFalse();
    }

    [Fact]
    public async Task The_pop_up_storage_request_is_still_editable_because_its_only_quote_is_a_draft()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);

        var response = await client.GetAsync($"/api/requests/{requestId}", ct);
        var detail = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);

        detail!.IsEditable.ShouldBeTrue();
    }

    [Fact]
    public async Task An_unknown_request_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync($"/api/requests/{Guid.NewGuid()}", ct);

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

    /// <summary>The shape of the 400 the built-in minimal API validation returns.</summary>
    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}

