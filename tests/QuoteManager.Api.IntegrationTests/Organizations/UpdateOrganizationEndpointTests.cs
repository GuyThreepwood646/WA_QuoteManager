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
/// Exercises <c>PUT /api/organizations/{organizationId}</c>, which updates an organization's
/// profile (name, contact fields, preferred-vendor flag, and locations). <c>Kind</c> is immutable.
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
    public async Task An_admin_can_update_an_organization_profile_including_locations_with_phone()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var uniqueName = $"Profile Update Target {Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = uniqueName, kind = "Vendor" },
            ct);
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<OrganizationListItem>(ct);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{created!.Id}",
            new
            {
                name = uniqueName,
                primaryAddress = "55 Crate Lane, Greensboro, NC 27409",
                primaryContactName = "Kim Olsen",
                primaryContactEmail = "kim.update@crateworks.test",
                primaryContactPhone = "+1 (336) 555-0163",
                isPreferredVendor = false,
                locations = new[]
                {
                    new { address = "18 Packing Court, Columbia, SC 29201", phone = "+1 (803) 555-0127" },
                    new { address = "9 Warehouse Row, Raleigh, NC 27603", phone = "+1 (919) 555-0199" },
                },
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrganizationListItem>(ct);

        body!.PrimaryContactEmail.ShouldBe("kim.update@crateworks.test");
        body.Locations.Count.ShouldBe(2);
        body.Locations.ShouldContain(l => l.Address.Contains("Raleigh") && l.Phone == "+1 (919) 555-0199");
    }

    [Fact]
    public async Task Marking_a_client_as_preferred_on_update_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var organizationId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}",
            new { name = "Meridian Pharma Sampling", isPreferredVendor = true },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("IsPreferredVendor");
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

    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}
