using QuoteManager.Api.IntegrationTests.Auth;

namespace QuoteManager.Api.IntegrationTests.Security;

/// <summary>
/// Enforces the baseline Content-Security-Policy as a test rather than prose: it must be present
/// on the SPA shell, static assets, and the JSON API alike, since this API is the actual security
/// boundary that also serves the built frontend (see Program.cs).
/// </summary>
public sealed class SecurityHeadersTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();
    private readonly HttpClient _client;

    public SecurityHeadersTests()
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
    [InlineData("/api/auth/me")]
    public async Task A_content_security_policy_header_is_present(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        var csp = GetCspHeader(response);
        csp.ShouldNotBeNull();
        csp.ShouldContain("default-src 'self'");
        csp.ShouldContain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task Scalars_dev_only_reference_page_is_excluded_since_it_loads_its_ui_from_a_cdn()
    {
        var response = await _client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        GetCspHeader(response).ShouldBeNull();
    }

    private static string? GetCspHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Content-Security-Policy", out var values) ? values.First() : null;
}
