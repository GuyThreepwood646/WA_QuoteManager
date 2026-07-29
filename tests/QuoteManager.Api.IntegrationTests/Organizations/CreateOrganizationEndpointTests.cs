using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuoteManager.Api.Auth;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Organizations;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Organizations;

/// <summary>
/// Exercises <c>POST /api/organizations</c>, which closes the gap where the only way an
/// organization could ever come into existence was the demo seeder.
/// </summary>
public sealed class CreateOrganizationEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_admin_can_create_an_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "New Freight Co", kind = "Vendor" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<OrganizationListItem>(ct);

        body.ShouldNotBeNull();
        body.Name.ShouldBe("New Freight Co");
        body.Kind.ShouldBe("Vendor");
        body.RetiredAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("requester@warehouseanywhere.test")]
    [InlineData("reviewer@warehouseanywhere.test")]
    [InlineData("vendor@warehouseanywhere.test")]
    public async Task A_non_admin_cannot_create_an_organization(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync(email);

        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Should Not Exist", kind = "Client" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("organization.action_not_permitted_for_role");
    }

    [Fact]
    public async Task A_duplicate_name_is_refused_as_a_domain_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Meridian Pharma Sampling", kind = "Client" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("organization.duplicate_name");
    }

    [Fact]
    public async Task A_blank_name_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "   ", kind = "Client" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Name");
    }

    [Fact]
    public async Task An_invalid_kind_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/organizations")
        {
            Content = JsonContent.Create(new { name = "Some Org", kind = 99 }),
        };

        var response = await client.SendAsync(message, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Kind");
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
