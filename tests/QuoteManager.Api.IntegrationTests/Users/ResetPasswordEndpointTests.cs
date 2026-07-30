using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuoteManager.Api.Auth;
using QuoteManager.Api.IntegrationTests.Auth;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.IntegrationTests.Users;

/// <summary>
/// Exercises <c>POST /api/users/{userId}/reset-password</c> - self-service requires proving the
/// current password; an admin resetting someone else's password does not, since they have no way
/// to supply one.
/// </summary>
public sealed class ResetPasswordEndpointTests : IDisposable
{
    private readonly QuoteManagerApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_user_can_reset_their_own_password_with_the_correct_current_password()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var userId = await FindUserIdAsync("requester@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/users/{userId}/reset-password",
            new
            {
                currentPassword = DemoDataSeeder.DemoPassword,
                newPassword = "NewStr0ng!Pass",
                confirmNewPassword = "NewStr0ng!Pass",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The old password no longer works; the new one does.
        var oldLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "requester@warehouseanywhere.test", password = DemoDataSeeder.DemoPassword },
            ct);
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "requester@warehouseanywhere.test", password = "NewStr0ng!Pass" },
            ct);
        newLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resetting_your_own_password_with_the_wrong_current_password_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var userId = await FindUserIdAsync("requester@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/users/{userId}/reset-password",
            new
            {
                currentPassword = "TotallyWrongPassword1!",
                newPassword = "NewStr0ng!Pass",
                confirmNewPassword = "NewStr0ng!Pass",
            },
            ct);

        // 403, not 401: the caller's own bearer token is perfectly valid here - a 401 would make
        // the SPA's apiClient treat this as a dead session and force a logout/redirect instead of
        // showing an inline "wrong password" error (apiClient.ts's blanket 401-means-logout rule).
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemCode>(ct);
        problem?.Code.ShouldBe("user.invalid_current_password");
    }

    [Fact]
    public async Task An_admin_can_reset_another_users_password_without_supplying_a_current_password()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("admin@warehouseanywhere.test");
        var userId = await FindUserIdAsync("vendor@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/users/{userId}/reset-password",
            new { newPassword = "AdminSet1!Pass", confirmNewPassword = "AdminSet1!Pass" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "vendor@warehouseanywhere.test", password = "AdminSet1!Pass" },
            ct);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_non_admin_cannot_reset_someone_elses_password()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var otherUserId = await FindUserIdAsync("reviewer@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/users/{otherUserId}/reset-password",
            new { newPassword = "Hijacked1!Pass", confirmNewPassword = "Hijacked1!Pass" },
            ct);

        // 404, not 403 - matches the same non-admin-can't-confirm-another-id's-existence rule
        // as PUT /api/users/{id}.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_weak_new_password_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var userId = await FindUserIdAsync("requester@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/users/{userId}/reset-password",
            new { currentPassword = DemoDataSeeder.DemoPassword, newPassword = "weak", confirmNewPassword = "weak" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("NewPassword");
    }

    [Fact]
    public async Task Mismatched_new_password_confirmation_is_rejected_as_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = await LoginAsAsync("requester@warehouseanywhere.test");
        var userId = await FindUserIdAsync("requester@warehouseanywhere.test", ct);

        var response = await client.PostAsJsonAsync(
            $"/api/users/{userId}/reset-password",
            new
            {
                currentPassword = DemoDataSeeder.DemoPassword,
                newPassword = "NewStr0ng!Pass",
                confirmNewPassword = "Different1!Pass",
            },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(ct);
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey("ConfirmNewPassword");
    }

    private async Task<Guid> FindUserIdAsync(string email, CancellationToken cancellationToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email, cancellationToken);
        return user.Id;
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
