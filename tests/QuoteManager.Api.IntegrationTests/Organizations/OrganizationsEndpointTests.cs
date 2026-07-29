using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Organizations;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Organizations;

/// <summary>
/// Exercises <c>GET /api/organizations</c>, the other endpoint sharing <c>PagedListQuery</c>
/// (<c>Api/Models</c>) with <c>GET /api/requests</c> - each list endpoint reuses one bound,
/// validated query type rather than repeating the shape.
/// </summary>
public sealed class OrganizationsEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Listing_organizations_with_no_query_string_returns_every_seeded_organization_with_the_default_page()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync("/api/organizations", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<OrganizationListItem>>(ct);

        page.ShouldNotBeNull();
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(25);
        page.Total.ShouldBe(5);
        page.Items.ShouldContain(o => o.Name == "Meridian Pharma Sampling" && o.Kind == "Client");
        page.Items.ShouldContain(o => o.Name == "Crateworks Packing & Crating" && o.Kind == "Vendor");
    }

    [Fact]
    public async Task Requesting_a_page_size_over_the_cap_is_clamped_rather_than_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync("/api/organizations?pageSize=500", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<OrganizationListItem>>(ct);

        page!.PageSize.ShouldBe(100);
    }

    [Fact]
    public async Task Requesting_a_negative_page_is_rejected_as_a_validation_problem_naming_the_field()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync("/api/organizations?page=-1", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest,
            "an explicit negative page has no sensible default to clamp to, unlike an absent one");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Page");
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
