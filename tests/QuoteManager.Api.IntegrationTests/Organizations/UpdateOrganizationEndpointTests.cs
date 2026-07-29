using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Organizations;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Organizations;

/// <summary>
/// Exercises <c>PUT /api/organizations/{organizationId}</c>, which renames an organization -
/// the only mutable business field the domain models (<c>Kind</c> is immutable).
/// </summary>
public sealed class UpdateOrganizationEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_admin_can_rename_an_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var organizationId = await FindOrganizationIdAsync("Interstate Freight Partners", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}",
            new { name = "Interstate Freight & Logistics" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrganizationListItem>(ct);
        body!.Name.ShouldBe("Interstate Freight & Logistics");
    }

    [Fact]
    public async Task A_non_admin_cannot_rename_an_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var organizationId = await FindOrganizationIdAsync("Interstate Freight Partners", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}",
            new { name = "Should Not Apply" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("organization.action_not_permitted_for_role");
    }

    [Fact]
    public async Task Renaming_to_another_organizations_existing_name_is_refused_as_a_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var organizationId = await FindOrganizationIdAsync("Interstate Freight Partners", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}",
            new { name = "SecureBase Self Storage" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("organization.duplicate_name");
    }

    [Fact]
    public async Task An_unknown_organization_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{Guid.NewGuid()}",
            new { name = "Doesn't Matter" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
