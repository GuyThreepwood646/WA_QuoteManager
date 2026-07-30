using System.Net.Http.Headers;
using System.Net.Http.Json;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Api.Users;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Users;

/// <summary>
/// Exercises <c>GET /api/users</c>'s visibility scoping - an Admin sees every user, but anyone
/// else's result is filtered to only their own row, the same "filter, don't 403" idiom
/// <c>RequestEndpoints.IsVendorOnlyView</c> already applies to quote visibility.
/// </summary>
public sealed class UsersEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task An_admin_sees_every_seeded_user_with_organization_names_resolved()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");

        var response = await client.GetAsync("/api/users?pageSize=100", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<UserListItem>>(ct);

        page.ShouldNotBeNull();
        page.Total.ShouldBe(6);

        var admin = page.Items.Single(u => u.Email == "admin@warehouseanywhere.test");
        admin.OrganizationId.ShouldBeNull();
        admin.OrganizationName.ShouldBeNull();

        var vendor = page.Items.Single(u => u.Email == "vendor@warehouseanywhere.test");
        vendor.OrganizationName.ShouldBe("SecureBase Self Storage");
        vendor.Address.ShouldNotBeNullOrWhiteSpace();
        vendor.Phone.ShouldNotBeNullOrWhiteSpace();
        vendor.Roles.ShouldBe(["Vendor"]);
    }

    [Fact]
    public async Task A_non_admin_sees_only_their_own_row()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");

        var response = await client.GetAsync("/api/users?pageSize=100", ct);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResult<UserListItem>>(ct);

        page.ShouldNotBeNull();
        page.Total.ShouldBe(1);
        page.Items.Single().Email.ShouldBe("requester@warehouseanywhere.test");
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
}
