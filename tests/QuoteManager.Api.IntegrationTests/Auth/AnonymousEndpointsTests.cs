using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuoteManager.Api.Auth;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Auth;

/// <summary>
/// Enforces the complete anonymous set as a test rather than prose: exactly the SPA fallback,
/// health, and the OpenAPI document are reachable without a token, and everything else is not.
/// </summary>
public sealed class AnonymousEndpointsTests : IDisposable
{
    private const string AdminEmail = "admin@warehouseanywhere.test";

    private readonly QuoteManagerApiFactory _factory = new();
    private readonly HttpClient _client;

    public AnonymousEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/openapi/v1.json")]
    public async Task Anonymous_routes_are_reachable_without_a_token(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{path} is in the enumerated anonymous set");
    }

    [Fact]
    public async Task A_representative_protected_route_returns_401_without_a_token()
    {
        var response = await _client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            "the fallback authorisation policy must protect everything not explicitly opened up");
    }

    [Fact]
    public async Task Logging_in_with_seeded_demo_credentials_issues_a_token_that_authorises_the_protected_route()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = AdminEmail, password = DemoDataSeeder.DemoPassword },
            TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.User.Roles.ShouldContain("Admin");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);

        var me = await _client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);

        var meBody = await me.Content.ReadFromJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken);
        meBody.ShouldNotBeNull();
        meBody.DisplayName.ShouldBe("Ada Admin");
    }

    [Fact]
    public async Task Logging_in_with_a_wrong_password_returns_401_with_a_stable_machine_code()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = AdminEmail, password = "not-the-password" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsBody>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Code.ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Logging_in_with_a_missing_password_is_rejected_as_a_validation_problem_naming_the_field()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = AdminEmail, password = "" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest,
            "the [Required] attribute on LoginRequest.Password should fail before the handler runs");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Password");
    }

    [Fact]
    public async Task Logging_in_with_a_whitespace_padded_email_is_rejected_as_a_validation_problem()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = $" {AdminEmail} ", password = DemoDataSeeder.DemoPassword },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest,
            "[EmailAddress] alone accepts a leading/trailing space - LoginRequest.Validate() must catch it");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Email");
    }

    /// <summary>Reads only the field this test cares about, since <c>ProblemDetails.Extensions</c> is dynamic.</summary>
    private sealed record ProblemDetailsBody(string Code);

    /// <summary>The shape of the 400 the built-in minimal API validation returns.</summary>
    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}
