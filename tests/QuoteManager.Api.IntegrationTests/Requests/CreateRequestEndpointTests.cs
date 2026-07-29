using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Requests;

/// <summary>
/// Exercises <c>POST /api/requests</c>, which closes the gap where the only way a request could
/// ever come into existence was the demo seeder.
/// </summary>
public sealed class CreateRequestEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_requester_can_raise_a_request_for_a_client_organisation()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var meridianId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct);

        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "New territory cold storage", description = "Pilot", clientOrganizationId = meridianId, neededBy = (DateTimeOffset?)null },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);

        body.ShouldNotBeNull();
        body.Title.ShouldBe("New territory cold storage");
        body.ClientOrganizationId.ShouldBe(meridianId);
        body.ClientOrganizationName.ShouldBe("Meridian Pharma Sampling");
        body.Status.ShouldBe("Open");
        body.IsEditable.ShouldBeTrue();
        body.Quotes.ShouldBeEmpty();
        body.Invitations.ShouldBeEmpty();

        // Persisted, not just echoed back.
        var stored = await client.GetAsync($"/api/requests/{body.Id}", ct);
        stored.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task An_admin_can_also_raise_a_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var palmettoId = await FindOrganizationIdAsync("Palmetto Retail & CPG", ct);

        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Spring reset staging", clientOrganizationId = palmettoId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_vendor_is_refused_with_403_and_the_stable_creation_denied_code()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor@warehouseanywhere.test");
        var meridianId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct);

        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Storage a vendor should not be able to raise", clientOrganizationId = meridianId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.creation_not_permitted");
    }

    [Fact]
    public async Task A_reviewer_is_refused_with_403_too()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");
        var meridianId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct);

        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Storage a reviewer should not be able to raise", clientOrganizationId = meridianId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_vendor_organisation_id_is_rejected_because_it_is_not_a_client()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var secureBaseId = await FindOrganizationIdAsync("SecureBase Self Storage", ct);

        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Wrong org kind", clientOrganizationId = secureBaseId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.unknown_client_organization");
    }

    [Fact]
    public async Task A_blank_title_is_rejected_as_a_validation_problem_naming_the_field()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var meridianId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct);

        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "   ", clientOrganizationId = meridianId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Title");
    }

    [Fact]
    public async Task An_empty_client_organisation_id_is_rejected_before_any_lookup_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/requests",
            new { title = "Missing client", clientOrganizationId = Guid.Empty },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("ClientOrganizationId");
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

    /// <summary>The shape of the 400 the built-in minimal API validation returns.</summary>
    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}
