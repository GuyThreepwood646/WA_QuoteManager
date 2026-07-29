using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Models;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Identity;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Auth;

public sealed record CurrentUserResponse(Guid Id, string DisplayName, IReadOnlyList<string> Roles, Guid? OrganizationId);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserResponse User);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        // The one login contract the SPA's apiClient depends on. Anonymous by design.
        group.MapPost("/login", LoginAsync).AllowAnonymous();

        // Lets the SPA rehydrate identity after a page refresh, and gives the anonymous-set test a
        // protected route to assert 401 against.
        group.MapGet("/me", (ICurrentUser currentUser) => Results.Ok(ToResponse(currentUser)));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        QuoteManagerDbContext db,
        IPasswordHasher<AppUser> passwordHasher,
        TokenService tokenService,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        var issued = tokenService.IssueFor(user);

        return Results.Ok(new LoginResponse(
            issued.AccessToken,
            issued.ExpiresAt,
            new CurrentUserResponse(user.Id, user.DisplayName, RoleNames(user.Roles), user.OrganizationId)));
    }

    private static IResult InvalidCredentials() => Results.Problem(
        title: "Invalid credentials",
        detail: "The email or password is incorrect.",
        statusCode: StatusCodes.Status401Unauthorized,
        extensions: new Dictionary<string, object?> { ["code"] = "auth.invalid_credentials" });

    private static CurrentUserResponse ToResponse(ICurrentUser currentUser) => new(
        currentUser.UserId,
        currentUser.DisplayName,
        RoleNames(currentUser.Roles),
        currentUser.OrganizationId);

    private static List<string> RoleNames(AppRole roles) =>
        roles.Split().Select(role => role.ToString()).ToList();
}
