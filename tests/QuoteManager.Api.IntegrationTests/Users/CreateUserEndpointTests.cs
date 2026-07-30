using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Users;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Users;

/// <summary>
/// Exercises <c>POST /api/users</c> - Admin-only, and the first place a password ever gets set
/// outside the demo seeder, so the password-complexity rules are enforced here for real.
/// </summary>
public sealed class CreateUserEndpointTests : IDisposable
{
    private static readonly string[] RequesterRole = ["Requester"];
    private static readonly string[] AdminRole = ["Admin"];
    private static readonly string[] InvalidRole = ["SuperAdmin"];

    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_admin_can_create_a_user_with_a_full_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var meridianId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct);

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = "new.requester@warehouseanywhere.test",
                displayName = "Nora Newperson",
                roles = RequesterRole,
                organizationId = meridianId,
                address = "500 Test Street, Atlanta, GA 30301",
                phone = "+1 (404) 555-0199",
                password = "Str0ng!Pass",
                confirmPassword = "Str0ng!Pass",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<UserListItem>(ct);

        body.ShouldNotBeNull();
        body.Email.ShouldBe("new.requester@warehouseanywhere.test");
        body.Roles.ShouldBe(["Requester"]);
        body.OrganizationName.ShouldBe("Meridian Pharma Sampling");
        body.Address.ShouldBe("500 Test Street, Atlanta, GA 30301");
    }

    [Fact]
    public async Task A_non_admin_cannot_create_a_user()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = "should.not.exist@warehouseanywhere.test",
                displayName = "Should Not Exist",
                roles = AdminRole,
                password = "Str0ng!Pass",
                confirmPassword = "Str0ng!Pass",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("user.action_not_permitted_for_role");
    }

    [Fact]
    public async Task A_duplicate_email_is_refused_as_a_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = "admin@warehouseanywhere.test",
                displayName = "Duplicate Admin",
                roles = AdminRole,
                password = "Str0ng!Pass",
                confirmPassword = "Str0ng!Pass",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("user.duplicate_email");
    }

    [Theory]
    [InlineData("short1!", "at least 8 characters")]
    [InlineData("alllowercase1!", "uppercase")]
    [InlineData("ALLUPPERCASE1!", "lowercase")]
    [InlineData("NoDigitsHere!", "number")]
    [InlineData("NoSpecial123", "special character")]
    public async Task A_password_missing_a_requirement_is_rejected_as_a_validation_problem(string weakPassword, string _)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = "weak.password@warehouseanywhere.test",
                displayName = "Weak Password",
                roles = AdminRole,
                password = weakPassword,
                confirmPassword = weakPassword,
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Password");
    }

    [Fact]
    public async Task Mismatched_confirm_password_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = "mismatch@warehouseanywhere.test",
                displayName = "Mismatch",
                roles = AdminRole,
                password = "Str0ng!Pass",
                confirmPassword = "Different!Pass1",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("ConfirmPassword");
    }

    [Fact]
    public async Task A_non_admin_only_role_without_an_organization_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = "no.org@warehouseanywhere.test",
                displayName = "No Org",
                roles = RequesterRole,
                password = "Str0ng!Pass",
                confirmPassword = "Str0ng!Pass",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("OrganizationId");
    }

    [Fact]
    public async Task An_invalid_role_name_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = "bad.role@warehouseanywhere.test",
                displayName = "Bad Role",
                roles = InvalidRole,
                password = "Str0ng!Pass",
                confirmPassword = "Str0ng!Pass",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Roles");
    }

    [Fact]
    public async Task An_unknown_organization_is_rejected_as_a_bad_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                email = "unknown.org@warehouseanywhere.test",
                displayName = "Unknown Org",
                roles = RequesterRole,
                organizationId = Guid.NewGuid(),
                password = "Str0ng!Pass",
                confirmPassword = "Str0ng!Pass",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("user.unknown_organization");
    }

    private async Task<Guid> FindOrganizationIdAsync(string name, CancellationToken cancellationToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
        var organization = await db.Organizations.AsNoTracking().SingleAsync(o => o.Name == name, cancellationToken);
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
