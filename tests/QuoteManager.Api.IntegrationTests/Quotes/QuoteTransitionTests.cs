using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Quotes;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Quotes;

/// <summary>
/// Exercises the single action-driven transition endpoint (AD-2) against the seeded demo data,
/// proving the role axis and permittedActions projection (AD-7) live entirely in
/// <c>QuoteTransitions</c> rather than in the endpoint.
/// </summary>
public sealed class QuoteTransitionTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_reviewer_may_start_review_on_a_submitted_quote_and_the_response_reflects_the_new_state()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Replace rooftop HVAC units", "vendor2@quotemgr.test");
        var client = await LoginAsAsync("reviewer@quotemgr.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);
        before.Status.ShouldBe("Submitted");
        before.PermittedActions.ShouldBe(["StartReview"]);

        var response = await SendActionAsync(client, requestId, quoteId, "StartReview", before.Version);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        after.ShouldNotBeNull();
        after.Status.ShouldBe("UnderReview");
        after.Version.ShouldBe(before.Version + 1);
        after.PermittedActions.ShouldBe(["Accept", "Reject", "ReturnToSubmitted"], ignoreOrder: true);
    }

    [Fact]
    public async Task A_vendor_may_not_start_review_and_gets_a_403_with_the_role_denied_code()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Replace rooftop HVAC units", "vendor2@quotemgr.test");
        var client = await LoginAsAsync("vendor2@quotemgr.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);

        var response = await SendActionAsync(client, requestId, quoteId, "StartReview", before.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.action_not_permitted_for_role");
    }

    [Fact]
    public async Task Accepting_a_quote_that_is_not_under_review_is_refused_as_a_domain_conflict()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Replace rooftop HVAC units", "vendor2@quotemgr.test");
        var client = await LoginAsAsync("reviewer@quotemgr.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);
        before.Status.ShouldBe("Submitted", "Accept is only legal from UnderReview - this is the case the demo deliberately exercises");

        var response = await SendActionAsync(client, requestId, quoteId, "Accept", before.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.transition_not_allowed");
    }

    [Fact]
    public async Task A_stale_If_Match_is_refused_as_a_concurrency_conflict()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Replace rooftop HVAC units", "vendor2@quotemgr.test");
        var client = await LoginAsAsync("reviewer@quotemgr.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);

        // Advances the quote past the version the caller is about to (incorrectly) assert.
        var first = await SendActionAsync(client, requestId, quoteId, "StartReview", before.Version);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stale = await SendActionAsync(client, requestId, quoteId, "Reject", before.Version);

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var code = await ReadProblemCodeAsync(stale);
        code.ShouldBe("quote.concurrent_modification");
    }

    [Fact]
    public async Task A_missing_If_Match_header_is_rejected_before_any_domain_logic_runs()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Replace rooftop HVAC units", "vendor2@quotemgr.test");
        var client = await LoginAsAsync("reviewer@quotemgr.test");

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quoteId}/actions")
        {
            Content = JsonContent.Create(new { action = "StartReview" }),
        };

        var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.if_match_required");
    }

    private async Task<(Guid RequestId, Guid QuoteId)> FindQuoteAsync(string requestTitle, string vendorEmail)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();

        var vendor = await db.Users.AsNoTracking().SingleAsync(u => u.Email == vendorEmail, cancellationToken);
        var request = await db.Requests.AsNoTracking().SingleAsync(r => r.Title == requestTitle, cancellationToken);
        var quote = request.Quotes.Single(q => q.VendorOrganizationId == vendor.OrganizationId);

        return (request.Id, quote.Id);
    }

    private async Task<HttpClient> LoginAsAsync(string email)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = DemoDataSeeder.DemoPassword },
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }

    private static async Task<QuoteResponse> GetQuoteAsync(HttpClient client, Guid requestId, Guid quoteId)
    {
        var response = await client.GetAsync(
            $"/api/requests/{requestId}/quotes/{quoteId}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken))!;
    }

    private static async Task<HttpResponseMessage> SendActionAsync(
        HttpClient client, Guid requestId, Guid quoteId, string action, int expectedVersion)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quoteId}/actions")
        {
            Content = JsonContent.Create(new { action }),
        };
        message.Headers.TryAddWithoutValidation("If-Match", $"\"{expectedVersion}\"");

        return await client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(TestContext.Current.CancellationToken);
        return problem?.Code;
    }

    private sealed record ProblemCode(string Code);
}
