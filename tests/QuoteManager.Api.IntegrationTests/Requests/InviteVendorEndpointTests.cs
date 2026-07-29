using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Requests;

/// <summary>
/// Exercises <c>POST /api/requests/{requestId}/invitations</c>, which closes the gap where
/// <c>Request.InviteVendor</c> was only ever called by the demo seeder - a real user could create
/// a request but never invite a vendor to quote on it.
/// </summary>
public sealed class InviteVendorEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_requester_can_invite_a_vendor_not_yet_invited()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);
        var secureBaseId = await FindOrganizationIdAsync("SecureBase Self Storage", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/invitations",
            new { vendorOrganizationId = secureBaseId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RequestDetailResponse>(ct);
        body!.Invitations.ShouldContain(i => i.VendorOrganizationName == "SecureBase Self Storage");
    }

    [Theory]
    [InlineData("vendor@warehouseanywhere.test")]
    [InlineData("reviewer@warehouseanywhere.test")]
    public async Task A_vendor_or_reviewer_cannot_invite_a_vendor(string email)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync(email);
        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);
        var secureBaseId = await FindOrganizationIdAsync("SecureBase Self Storage", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/invitations",
            new { vendorOrganizationId = secureBaseId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.action_not_permitted_for_role");
    }

    [Fact]
    public async Task Inviting_a_client_kind_organization_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);
        var meridianId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/invitations",
            new { vendorOrganizationId = meridianId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.unknown_vendor_organization");
    }

    [Fact]
    public async Task Inviting_the_same_vendor_twice_is_an_idempotent_no_op()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Pop-up retail storage & fixture staging", ct);
        var crateworksId = await FindOrganizationIdAsync("Crateworks Packing & Crating", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/invitations",
            new { vendorOrganizationId = crateworksId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Inviting_a_vendor_to_a_request_that_is_no_longer_Open_is_refused_as_a_domain_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var requestId = await FindRequestIdAsync("Trade show fixture storage & drayage — West Coast expo season", ct);
        var secureBaseId = await FindOrganizationIdAsync("SecureBase Self Storage", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{requestId}/invitations",
            new { vendorOrganizationId = secureBaseId },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("request.not_editable");
    }

    [Fact]
    public async Task An_unknown_request_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var secureBaseId = await FindOrganizationIdAsync("SecureBase Self Storage", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/requests/{Guid.NewGuid()}/invitations",
            new { vendorOrganizationId = secureBaseId },
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

    private async Task<Guid> FindRequestIdAsync(string title, CancellationToken ct)
    {
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var response = await client.GetAsync("/api/requests?pageSize=100", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<RequestListItem>>(ct);

        return page!.Items.Single(r => r.Title == title).Id;
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
}
