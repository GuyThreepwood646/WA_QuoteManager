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
/// Exercises <c>PUT /api/requests/{requestId}</c>, which closes the gap where a request could be
/// created but never corrected.
/// </summary>
public sealed class UpdateRequestEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_requester_can_update_an_editable_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/requests/{requestId}",
            new { title = "Pop-up retail storage & fixture staging (revised)", description = "Updated scope", neededBy = (DateTimeOffset?)null },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);
        body!.Title.ShouldBe("Pop-up retail storage & fixture staging (revised)");
        body.Description.ShouldBe("Updated scope");
    }

    [Fact]
    public async Task An_admin_can_also_update_a_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/requests/{requestId}",
            new { title = "Renamed by admin" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("vendor@warehouseanywhere.test")]
    [InlineData("reviewer@warehouseanywhere.test")]
    public async Task A_vendor_or_reviewer_cannot_update_a_request(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync(email);
        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/requests/{requestId}",
            new { title = "Should not apply" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.action_not_permitted_for_role");
    }

    [Fact]
    public async Task Updating_a_request_that_is_no_longer_editable_is_refused_as_a_domain_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Regional sample storage — Southeast territory", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/requests/{requestId}",
            new { title = "Should be refused" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.not_editable");
    }

    [Fact]
    public async Task A_blank_title_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/requests/{requestId}",
            new { title = "   " },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Title");
    }

    [Fact]
    public async Task An_unknown_request_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");

        var response = await client.PutAsJsonAsync(
            $"/api/requests/{Guid.NewGuid()}",
            new { title = "Doesn't matter" },
            ct);

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

    /// <summary>The shape of the 400 the built-in minimal API validation returns.</summary>
    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}
