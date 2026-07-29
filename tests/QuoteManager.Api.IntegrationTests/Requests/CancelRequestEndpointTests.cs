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
/// Exercises <c>POST /api/requests/{requestId}/cancel</c>, which closes the gap where a request
/// that didn't work out could never be called off - only ever awarded or left open forever.
/// </summary>
public sealed class CancelRequestEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_requester_can_cancel_an_open_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Cold-chain sample storage pilot — new territory launch", ct);

        var response = await client.PostAsync($"/api/requests/{requestId}/cancel", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);
        body!.Status.ShouldBe("Cancelled");
    }

    [Theory]
    [InlineData("vendor@warehouseanywhere.test")]
    [InlineData("reviewer@warehouseanywhere.test")]
    public async Task A_vendor_or_reviewer_cannot_cancel_a_request(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync(email);
        var requestId = await FindRequestIdAsync("Cold-chain sample storage pilot — new territory launch", ct);

        var response = await client.PostAsync($"/api/requests/{requestId}/cancel", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.action_not_permitted_for_role");
    }

    [Fact]
    public async Task An_awarded_request_cannot_be_cancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Trade show fixture storage & drayage — West Coast expo season", ct);

        var response = await client.PostAsync($"/api/requests/{requestId}/cancel", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.not_editable");
    }

    [Fact]
    public async Task Cancelling_an_already_cancelled_request_is_an_idempotent_no_op()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Cold-chain sample storage pilot — new territory launch", ct);

        var first = await client.PostAsync($"/api/requests/{requestId}/cancel", null, ct);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await client.PostAsync($"/api/requests/{requestId}/cancel", null, ct);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unknown_request_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsync($"/api/requests/{Guid.NewGuid()}/cancel", null, ct);

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

    private sealed record ProblemCode(string Code);
}
