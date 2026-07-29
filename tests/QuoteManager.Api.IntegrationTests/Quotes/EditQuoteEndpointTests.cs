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
/// Exercises <c>PUT /api/requests/{requestId}/quotes/{quoteId}</c>, editing an already-drafted
/// quote's business fields. <c>Request.EditQuote</c> resolves the same <c>QuoteTransitions</c>
/// table the status-transition endpoint uses, so ownership and the Draft-only rule are already
/// covered there - this asserts the endpoint wires that through correctly, not the rule itself.
/// </summary>
public sealed class EditQuoteEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_withdrawn_quote_can_be_revised_back_to_draft()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Pop-up retail storage & fixture staging", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var draft = await GetQuoteAsync(client, requestId, quoteId);
        using var withdraw = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quoteId}/transitions")
        {
            Content = JsonContent.Create(new { action = "Withdraw" }),
        };
        withdraw.Headers.TryAddWithoutValidation("If-Match", $"\"{draft.Version}\"");
        (await client.SendAsync(withdraw, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var withdrawn = await GetQuoteAsync(client, requestId, quoteId);
        withdrawn.Status.ShouldBe("Withdrawn");

        var response = await SendEditAsync(
            client, requestId, quoteId, withdrawn.Version,
            new { amount = 14_500m, currency = "USD", notes = "Revised after withdrawal" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var revised = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        revised.ShouldNotBeNull();
        revised.Status.ShouldBe("Draft");
        revised.PermittedActions.ShouldContain("Submit");
    }

    [Fact]
    public async Task A_rejected_quote_can_be_revised_back_to_draft()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var reviewer = await LoginAsAsync("reviewer@warehouseanywhere.test");
        var vendor = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var submitted = await GetQuoteAsync(reviewer, requestId, quoteId);
        submitted.Status.ShouldBe("Submitted");

        using var startReview = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quoteId}/transitions")
        {
            Content = JsonContent.Create(new { action = "StartReview" }),
        };
        startReview.Headers.TryAddWithoutValidation("If-Match", $"\"{submitted.Version}\"");
        (await reviewer.SendAsync(startReview, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var underReview = await GetQuoteAsync(reviewer, requestId, quoteId);
        underReview.Status.ShouldBe("UnderReview");

        using var reject = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quoteId}/transitions")
        {
            Content = JsonContent.Create(new { action = "Reject" }),
        };
        reject.Headers.TryAddWithoutValidation("If-Match", $"\"{underReview.Version}\"");
        (await reviewer.SendAsync(reject, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        var rejected = await GetQuoteAsync(vendor, requestId, quoteId);
        rejected.Status.ShouldBe("Rejected");

        var response = await SendEditAsync(
            vendor, requestId, quoteId, rejected.Version,
            new { amount = 11_250m, currency = "USD", notes = "Revised after rejection" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var revised = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);
        revised.ShouldNotBeNull();
        revised.Status.ShouldBe("Draft");
    }

    [Fact]
    public async Task The_owning_vendor_can_edit_its_own_draft_quote()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Pop-up retail storage & fixture staging", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);
        before.Status.ShouldBe("Draft");

        var response = await SendEditAsync(
            client, requestId, quoteId, before.Version,
            new { amount = 15_250m, currency = "USD", notes = "Revised after site visit" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await response.Content.ReadFromJsonAsync<QuoteResponse>(TestContext.Current.CancellationToken);

        after.ShouldNotBeNull();
        after.Amount.ShouldBe(15_250m);
        after.Notes.ShouldBe("Revised after site visit");
        after.Version.ShouldBe(before.Version + 1);
    }

    [Fact]
    public async Task Admin_can_edit_a_quote_on_behalf_of_the_vendor()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Pop-up retail storage & fixture staging", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);

        var response = await SendEditAsync(
            client, requestId, quoteId, before.Version,
            new { amount = 16_000m, currency = "USD" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_different_vendor_cannot_edit_someone_elses_draft_quote()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Pop-up retail storage & fixture staging", "vendor2@warehouseanywhere.test");
        var competitor = await LoginAsAsync("vendor@warehouseanywhere.test");

        var before = await GetQuoteAsync(competitor, requestId, quoteId);

        var response = await SendEditAsync(
            competitor, requestId, quoteId, before.Version,
            new { amount = 1m, currency = "USD" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.action_not_permitted_for_role");
    }

    [Fact]
    public async Task Editing_a_quote_that_has_progressed_past_Draft_is_refused_as_a_domain_conflict()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);
        before.Status.ShouldBe("Submitted", "Edit is only legal from Draft");

        var response = await SendEditAsync(
            client, requestId, quoteId, before.Version,
            new { amount = 1m, currency = "USD" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.transition_not_allowed");
    }

    [Fact]
    public async Task A_stale_If_Match_is_refused_as_a_concurrency_conflict()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Pop-up retail storage & fixture staging", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);

        var first = await SendEditAsync(
            client, requestId, quoteId, before.Version, new { amount = 100m, currency = "USD" });
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stale = await SendEditAsync(
            client, requestId, quoteId, before.Version, new { amount = 200m, currency = "USD" });

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var code = await ReadProblemCodeAsync(stale);
        code.ShouldBe("quote.concurrent_modification");
    }

    [Fact]
    public async Task A_missing_If_Match_header_is_rejected_before_any_domain_logic_runs()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Pop-up retail storage & fixture staging", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        using var message = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/requests/{requestId}/quotes/{quoteId}")
        {
            Content = JsonContent.Create(new { amount = 100m, currency = "USD" }),
        };

        var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.if_match_required");
    }

    [Fact]
    public async Task An_unknown_quote_id_returns_404()
    {
        var request = await FindRequestIdAsync("Pop-up retail storage & fixture staging");
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var response = await SendEditAsync(
            client, request, Guid.NewGuid(), expectedVersion: 0, new { amount = 100m, currency = "USD" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.not_found");
    }

    [Fact]
    public async Task A_zero_amount_is_rejected_as_a_validation_problem()
    {
        var (requestId, quoteId) = await FindQuoteAsync(
            "Pop-up retail storage & fixture staging", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");
        var before = await GetQuoteAsync(client, requestId, quoteId);

        var response = await SendEditAsync(
            client, requestId, quoteId, before.Version, new { amount = 0m, currency = "USD" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Amount");
    }

    private async Task<Guid> FindRequestIdAsync(string requestTitle)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
        var request = await db.Requests.AsNoTracking()
            .SingleAsync(r => r.Title == requestTitle, TestContext.Current.CancellationToken);
        return request.Id;
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

    private static async Task<HttpResponseMessage> SendEditAsync(
        HttpClient client, Guid requestId, Guid quoteId, int expectedVersion, object body)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/requests/{requestId}/quotes/{quoteId}")
        {
            Content = JsonContent.Create(body),
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

    /// <summary>The shape of the 400 the built-in minimal API validation returns.</summary>
    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}
