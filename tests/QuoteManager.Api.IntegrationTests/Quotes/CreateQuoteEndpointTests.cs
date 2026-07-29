using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Quotes;
using QuoteManager.Api.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Quotes;

/// <summary>
/// Exercises <c>POST /api/requests/{requestId}/quotes</c>, which closes the gap where the only
/// way a quote could ever come into existence was the demo seeder.
/// </summary>
public sealed class CreateQuoteEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_admin_can_draft_a_quote_on_behalf_of_any_vendor()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var before = await client.GetFromJsonAsync<RequestDetailResponse>($"/api/requests/{requestId}", ct);
        before!.CanAddQuote.ShouldBeTrue("Admin drafts on behalf of vendors on any open request");

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 2100m, currency = "USD", notes = "Entered for Interstate" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<QuoteResponse>(ct);

        body.ShouldNotBeNull();
        body.VendorOrganizationId.ShouldBe(interstateId);
        body.Status.ShouldBe("Draft");
        body.Amount.ShouldBe(2100m);
    }

    [Fact]
    public async Task A_vendor_can_draft_again_after_a_reviewer_rejects_its_quote()
    {
        var ct = TestContext.Current.CancellationToken;
        var vendorClient = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var reviewerClient = await LoginAsAsync("reviewer@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var created = await vendorClient.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 500m, currency = "USD" },
            ct);
        created.EnsureSuccessStatusCode();
        var quote = await created.Content.ReadFromJsonAsync<QuoteResponse>(ct);

        using var submit = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quote!.Id}/transitions")
        {
            Content = JsonContent.Create(new { action = "Submit" }),
        };
        submit.Headers.TryAddWithoutValidation("If-Match", $"\"{quote.Version}\"");
        (await vendorClient.SendAsync(submit, ct)).EnsureSuccessStatusCode();

        var submitted = await vendorClient.GetFromJsonAsync<QuoteResponse>(
            $"/api/requests/{requestId}/quotes/{quote.Id}", ct);

        using var startReview = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quote.Id}/transitions")
        {
            Content = JsonContent.Create(new { action = "StartReview" }),
        };
        startReview.Headers.TryAddWithoutValidation("If-Match", $"\"{submitted!.Version}\"");
        (await reviewerClient.SendAsync(startReview, ct)).EnsureSuccessStatusCode();

        var underReview = await vendorClient.GetFromJsonAsync<QuoteResponse>(
            $"/api/requests/{requestId}/quotes/{quote.Id}", ct);

        using var reject = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quote.Id}/transitions")
        {
            Content = JsonContent.Create(new { action = "Reject" }),
        };
        reject.Headers.TryAddWithoutValidation("If-Match", $"\"{underReview!.Version}\"");
        (await reviewerClient.SendAsync(reject, ct)).EnsureSuccessStatusCode();

        var detail = await vendorClient.GetFromJsonAsync<RequestDetailResponse>($"/api/requests/{requestId}", ct);
        detail!.CanAddQuote.ShouldBeTrue("a rejected quote must not block drafting again");

        var secondDraft = await vendorClient.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 650m, currency = "USD" },
            ct);
        secondDraft.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_vendor_can_draft_again_after_withdrawing_their_only_quote()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var created = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 500m, currency = "USD" },
            ct);
        created.EnsureSuccessStatusCode();
        var quote = await created.Content.ReadFromJsonAsync<QuoteResponse>(ct);

        using var withdraw = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quote!.Id}/transitions")
        {
            Content = JsonContent.Create(new { action = "Withdraw" }),
        };
        withdraw.Headers.TryAddWithoutValidation("If-Match", $"\"{quote.Version}\"");
        (await client.SendAsync(withdraw, ct)).EnsureSuccessStatusCode();

        var detail = await client.GetFromJsonAsync<RequestDetailResponse>($"/api/requests/{requestId}", ct);
        detail!.CanAddQuote.ShouldBeTrue("a withdrawn quote must not block drafting again");

        var secondDraft = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 600m, currency = "USD" },
            ct);
        secondDraft.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_admin_can_draft_for_a_vendor_again_after_that_vendor_withdrew()
    {
        var ct = TestContext.Current.CancellationToken;
        var vendorClient = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var adminClient = await LoginAsAsync("admin@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var created = await vendorClient.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 500m, currency = "USD" },
            ct);
        created.EnsureSuccessStatusCode();
        var quote = await created.Content.ReadFromJsonAsync<QuoteResponse>(ct);

        using var withdraw = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/requests/{requestId}/quotes/{quote!.Id}/transitions")
        {
            Content = JsonContent.Create(new { action = "Withdraw" }),
        };
        withdraw.Headers.TryAddWithoutValidation("If-Match", $"\"{quote.Version}\"");
        (await vendorClient.SendAsync(withdraw, ct)).EnsureSuccessStatusCode();

        var detail = await adminClient.GetFromJsonAsync<RequestDetailResponse>($"/api/requests/{requestId}", ct);
        detail!.CanAddQuote.ShouldBeTrue();

        var adminDraft = await adminClient.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 900m, currency = "USD" },
            ct);
        adminDraft.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_vendor_can_draft_a_quote_for_its_own_organization_on_an_open_request()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 1234.5m, currency = "USD", notes = "Refrigerated bay available" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<QuoteResponse>(ct);

        body.ShouldNotBeNull();
        body.RequestId.ShouldBe(requestId);
        body.VendorOrganizationId.ShouldBe(interstateId);
        body.Status.ShouldBe("Draft");
        body.Amount.ShouldBe(1234.50m);
        body.Currency.ShouldBe("USD");
        body.PermittedActions.ShouldContain("Submit");
    }

    [Fact]
    public async Task A_vendor_cannot_draft_a_quote_under_a_competitors_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var (requestId, secureBaseId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = secureBaseId, amount = 500m, currency = "USD" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("quote.action_not_permitted_for_role");
    }

    [Fact]
    public async Task A_requester_cannot_draft_a_quote_at_all()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var (requestId, secureBaseId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = secureBaseId, amount = 500m, currency = "USD" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Drafting_a_quote_on_an_awarded_request_is_refused_as_a_domain_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Trade show fixture storage & drayage — West Coast expo season", "vendor3@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 100m, currency = "USD" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.not_editable");
    }

    [Fact]
    public async Task A_negative_amount_is_rejected_as_a_validation_problem_before_any_domain_logic_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = -1m, currency = "USD" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Amount");
    }

    [Fact]
    public async Task A_zero_amount_is_rejected_as_a_validation_problem()
    {
        // A storage/packing/freight quote worth exactly nothing is never a real offer - only ever
        // a client bug or an empty form field slipping past a required check.
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 0m, currency = "USD" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Amount");
    }

    [Fact]
    public async Task A_currency_that_is_not_three_letters_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 100m, currency = "US" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("Currency");
    }

    [Fact]
    public async Task An_unknown_request_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{Guid.NewGuid()}/quotes",
            new { vendorOrganizationId = Guid.NewGuid(), amount = 100m, currency = "USD" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_new_quote_makes_can_add_quote_false_on_a_subsequent_read_by_the_same_vendor()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("vendor3@warehouseanywhere.test");
        var (requestId, interstateId) = await FindRequestAndVendorAsync(
            "Cold-chain sample storage pilot — new territory launch", "vendor3@warehouseanywhere.test", ct);

        var before = await client.GetFromJsonAsync<RequestDetailResponse>($"/api/requests/{requestId}", ct);
        before!.CanAddQuote.ShouldBeTrue("Interstate was invited and has not quoted yet");

        var created = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/quotes",
            new { vendorOrganizationId = interstateId, amount = 777m, currency = "USD" },
            ct);
        created.EnsureSuccessStatusCode();

        var after = await client.GetFromJsonAsync<RequestDetailResponse>($"/api/requests/{requestId}", ct);
        after!.CanAddQuote.ShouldBeFalse("Interstate now has a quote on this request");
    }

    private async Task<(Guid RequestId, Guid VendorOrganizationId)> FindRequestAndVendorAsync(
        string requestTitle, string vendorEmail, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();

        var vendor = await db.Users.AsNoTracking().SingleAsync(u => u.Email == vendorEmail, ct);
        var request = await db.Requests.AsNoTracking().SingleAsync(r => r.Title == requestTitle, ct);

        return (request.Id, vendor.OrganizationId!.Value);
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
