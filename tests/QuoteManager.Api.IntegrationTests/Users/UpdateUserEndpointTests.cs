using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Users;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Users;

/// <summary>
/// Exercises <c>PUT /api/users/{userId}</c> - self-service editing is allowed for anyone editing
/// their own account, but role/organization changes and editing someone else both require Admin.
/// </summary>
public sealed class UpdateUserEndpointTests : IDisposable
{
    private static readonly string[] RequesterRole = ["Requester"];
    private static readonly string[] ReviewerRole = ["Reviewer"];
    private static readonly string[] AdminRole = ["Admin"];
    private static readonly string[] VendorAndReviewerRoles = ["Vendor", "Reviewer"];

    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_user_can_edit_their_own_profile_and_the_response_includes_a_fresh_token()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var userId = await FindUserIdAsync("requester@warehouseanywhere.test", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/users/{userId}",
            new
            {
                email = "requester@warehouseanywhere.test",
                displayName = "Riley Updated",
                address = "New Address, Atlanta, GA 30301",
                phone = "+1 (404) 555-0100",
                roles = RequesterRole,
                organizationId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct),
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UpdateUserResponse>(ct);

        body.ShouldNotBeNull();
        body.User.DisplayName.ShouldBe("Riley Updated");
        body.User.Address.ShouldBe("New Address, Atlanta, GA 30301");
        body.AccessToken.ShouldNotBeNullOrWhiteSpace("editing yourself must reissue a token so the header doesn't show a stale display name");
        body.ExpiresAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task An_admin_can_edit_another_users_profile_and_roles()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var userId = await FindUserIdAsync("vendor3@warehouseanywhere.test", ct);
        var interstateId = await FindOrganizationIdAsync("Interstate Freight Partners", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/users/{userId}",
            new
            {
                email = "vendor3@warehouseanywhere.test",
                displayName = "Rob Promoted",
                roles = VendorAndReviewerRoles,
                organizationId = interstateId,
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UpdateUserResponse>(ct);

        body.ShouldNotBeNull();
        body.User.DisplayName.ShouldBe("Rob Promoted");
        body.User.Roles.ShouldBe(["Reviewer", "Vendor"], ignoreOrder: true);
        body.AccessToken.ShouldBeNull("only editing your own account reissues a token");
    }

    [Fact]
    public async Task A_non_admin_cannot_edit_someone_elses_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var otherUserId = await FindUserIdAsync("reviewer@warehouseanywhere.test", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/users/{otherUserId}",
            new
            {
                email = "reviewer@warehouseanywhere.test",
                displayName = "Hijacked",
                roles = ReviewerRole,
                organizationId = await FindOrganizationIdAsync("Palmetto Retail & CPG", ct),
            },
            ct);

        // 404, not 403 - a non-admin shouldn't be able to confirm another user's id is real.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_non_admin_cannot_change_their_own_roles_or_organization()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var userId = await FindUserIdAsync("requester@warehouseanywhere.test", ct);
        var palmettoId = await FindOrganizationIdAsync("Palmetto Retail & CPG", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/users/{userId}",
            new
            {
                email = "requester@warehouseanywhere.test",
                displayName = "Riley Requester",
                roles = AdminRole,
                organizationId = palmettoId,
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
        var userId = await FindUserIdAsync("requester@warehouseanywhere.test", ct);

        var response = await client.PutAsJsonAsync(
            $"/api/users/{userId}",
            new
            {
                email = "reviewer@warehouseanywhere.test",
                displayName = "Riley Requester",
                roles = RequesterRole,
                organizationId = await FindOrganizationIdAsync("Meridian Pharma Sampling", ct),
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("user.duplicate_email");
    }

    [Fact]
    public async Task An_unknown_user_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.PutAsJsonAsync(
            $"/api/users/{Guid.NewGuid()}",
            new
            {
                email = "ghost@warehouseanywhere.test",
                displayName = "Ghost",
                roles = AdminRole,
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<Guid> FindUserIdAsync(string email, CancellationToken cancellationToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email, cancellationToken);
        return user.Id;
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
}
