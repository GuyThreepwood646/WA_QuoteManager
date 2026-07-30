using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Quotes;
using QuoteManager.Api.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Quotes;

/// <summary>
/// Exercises the single action-driven transition endpoint against the seeded demo data, proving
/// the role axis and permittedActions projection live entirely in <c>QuoteTransitions</c> rather
/// than in the endpoint.
/// </summary>
public sealed class QuoteTransitionTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_reviewer_may_start_review_on_a_submitted_quote_and_the_response_reflects_the_new_state()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

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
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("vendor2@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);

        var response = await SendActionAsync(client, requestId, quoteId, "StartReview", before.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.action_not_permitted_for_role");
    }

    [Fact]
    public async Task Accepting_a_quote_that_is_not_under_review_is_refused_as_a_domain_conflict()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);
        before.Status.ShouldBe("Submitted", "Accept is only legal from UnderReview - this is the case the demo deliberately exercises");

        var response = await SendActionAsync(client, requestId, quoteId, "Accept", before.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.transition_not_allowed");
    }

    [Fact]
    public async Task Accepting_a_quote_rejects_competing_quotes_and_leaves_them_read_only_on_the_request_detail()
    {
        var ct = TestContext.Current.CancellationToken;
        var (requestId, winnerQuoteId) = await FindQuoteAsync(
            "Regional sample storage — Southeast territory", "vendor@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var winner = await GetQuoteAsync(client, requestId, winnerQuoteId);
        winner.Status.ShouldBe("UnderReview");

        var accept = await SendActionAsync(client, requestId, winnerQuoteId, "Accept", winner.Version);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);

        var detail = await client.GetFromJsonAsync<RequestDetailResponse>($"/api/requests/{requestId}", ct);
        detail.ShouldNotBeNull();
        detail.Status.ShouldBe("Awarded");
        detail.CanAddQuote.ShouldBeFalse();
        detail.CanEdit.ShouldBeFalse();
        detail.CanCancel.ShouldBeFalse();

        var accepted = detail.Quotes.Single(q => q.Id == winnerQuoteId);
        accepted.Status.ShouldBe("Accepted");
        accepted.PermittedActions.ShouldBeEmpty();

        var rejected = detail.Quotes.Single(q => q.VendorOrganizationName == "Crateworks Packing & Crating");
        rejected.Status.ShouldBe("Rejected");
        rejected.StatusReason.ShouldBe("SupersededByAcceptedQuote");
        rejected.PermittedActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_stale_If_Match_is_refused_as_a_concurrency_conflict()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

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
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quoteId}/transitions")
        {
            Content = JsonContent.Create(new { action = "StartReview" }),
        };

        var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.if_match_required");
    }

    [Fact]
    public async Task A_vendor_may_not_withdraw_another_vendors_quote()
    {
        // Seed: the sample-storage request has SecureBase UnderReview and Crateworks Submitted.
        // Without the vendor ownership check, SecureBase (vendor@) could Withdraw Crateworks's
        // quote because both share the Vendor role.
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var competitor = await LoginAsAsync("vendor@warehouseanywhere.test");

        var before = await GetQuoteAsync(competitor, requestId, quoteId);
        before.Status.ShouldBe("Submitted");
        before.PermittedActions.ShouldBeEmpty(
            "permittedActions must also refuse ownership mismatches, or the UI would offer a button the API rejects");

        var response = await SendActionAsync(competitor, requestId, quoteId, "Withdraw", before.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var code = await ReadProblemCodeAsync(response);
        code.ShouldBe("quote.action_not_permitted_for_role");
    }

    [Fact]
    public async Task A_note_supplied_with_a_transition_appears_on_the_request_activity_timeline()
    {
        var ct = TestContext.Current.CancellationToken;
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);
        var response = await SendActionAsync(
            client, requestId, quoteId, "StartReview", before.Version, "Looks reasonable, checking references first.");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var activity = await client.GetFromJsonAsync<PagedResult<ActivityEntryResponse>>(
            $"/api/requests/{requestId}/activity?pageSize=100", ct);
        activity.ShouldNotBeNull();

        var entry = activity.Items.Single(e => e.SubjectId == quoteId && e.Action == "QuoteUnderReview");
        entry.Note.ShouldBe("Looks reasonable, checking references first.");
        // The note is its own field on the timeline entry, not folded into the summary sentence.
        entry.Summary.ShouldNotContain("Looks reasonable");
    }

    [Fact]
    public async Task A_transition_without_a_note_leaves_the_activity_entrys_note_null()
    {
        var ct = TestContext.Current.CancellationToken;
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);
        var response = await SendActionAsync(client, requestId, quoteId, "StartReview", before.Version);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var activity = await client.GetFromJsonAsync<PagedResult<ActivityEntryResponse>>(
            $"/api/requests/{requestId}/activity?pageSize=100", ct);
        activity.ShouldNotBeNull();

        var entry = activity.Items.Single(e => e.SubjectId == quoteId && e.Action == "QuoteUnderReview");
        entry.Note.ShouldBeNull();
    }

    [Fact]
    public async Task An_overlong_note_is_rejected_as_a_validation_problem()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);
        var response = await SendActionAsync(client, requestId, quoteId, "StartReview", before.Version, new string('x', 2001));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Note");
    }

    [Fact]
    public async Task Applying_an_action_outside_the_defined_enum_is_rejected_as_a_validation_problem_before_any_domain_logic_runs()
    {
        var (requestId, quoteId) = await FindQuoteAsync("Regional sample storage — Southeast territory", "vendor2@warehouseanywhere.test");
        var client = await LoginAsAsync("reviewer@warehouseanywhere.test");

        var before = await GetQuoteAsync(client, requestId, quoteId);

        // JsonStringEnumConverter still accepts a bare integer on deserialisation, so this binds
        // successfully to an undefined QuoteAction - only ApplyQuoteActionRequest.Validate() stops it.
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quoteId}/transitions")
        {
            Content = JsonContent.Create(new { action = 999 }),
        };
        message.Headers.TryAddWithoutValidation("If-Match", $"\"{before.Version}\"");

        var response = await client.SendAsync(message, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Action");
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
        HttpClient client, Guid requestId, Guid quoteId, string action, int expectedVersion, string? note = null)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quoteId}/transitions")
        {
            Content = JsonContent.Create(new { action, note }),
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
