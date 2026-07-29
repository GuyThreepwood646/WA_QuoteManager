using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Organizations;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Organizations;

/// <summary>
/// Exercises <c>POST /api/organizations/{organizationId}/retire</c> (soft delete), and the
/// <c>includeRetired</c> filter on <c>GET /api/organizations</c> that keeps a retired
/// organization out of pickers while leaving it visible to Admin.
/// </summary>
public sealed class RetireOrganizationEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_admin_can_retire_an_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var organizationId = await FindOrganizationIdAsync("Interstate Freight Partners", ct);

        var response = await client.PostAsync($"/api/organizations/{organizationId}/retire", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrganizationListItem>(ct);
        body!.RetiredAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_non_admin_cannot_retire_an_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var organizationId = await FindOrganizationIdAsync("Interstate Freight Partners", ct);

        var response = await client.PostAsync($"/api/organizations/{organizationId}/retire", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("organization.action_not_permitted_for_role");
    }

    [Fact]
    public async Task An_unknown_organization_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsync($"/api/organizations/{Guid.NewGuid()}/retire", null, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_retired_organization_is_excluded_from_the_default_list_but_included_with_includeRetired()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var organizationId = await FindOrganizationIdAsync("Interstate Freight Partners", ct);

        var retire = await client.PostAsync($"/api/organizations/{organizationId}/retire", null, ct);
        retire.EnsureSuccessStatusCode();

        var defaultList = await client.GetFromJsonAsync<PagedResult<OrganizationListItem>>(
            "/api/organizations?pageSize=100", ct);
        defaultList!.Items.ShouldNotContain(o => o.Id == organizationId);

        var withRetired = await client.GetFromJsonAsync<PagedResult<OrganizationListItem>>(
            "/api/organizations?pageSize=100&includeRetired=true", ct);
        withRetired!.Items.ShouldContain(o => o.Id == organizationId);
    }

    private async Task<Guid> FindOrganizationIdAsync(string name, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
        var organization = await db.Organizations.AsNoTracking().SingleAsync(o => o.Name == name, ct);
        return organization.Id;
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
