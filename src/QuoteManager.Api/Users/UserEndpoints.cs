using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Auth;
using QuoteManager.Api.Common;
using QuoteManager.Api.Models;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Users;

public sealed record UserListItem(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    Guid? OrganizationId,
    string? OrganizationName,
    string? Address,
    string? Phone);

/// <summary>
/// Only populated when the edited user is the caller themselves - <c>DisplayName</c>/<c>Email</c>/
/// <c>Roles</c> are baked into the JWT at login and never re-read from the database per request
/// (see <see cref="TokenService"/>), so without a fresh token a self-edit would keep showing stale
/// values in the header until the next login. Editing someone else's account has no such fix here:
/// that person's own session goes stale until they next log in, which is accepted, documented
/// behaviour rather than something this endpoint tries to solve.
/// </summary>
public sealed record UpdateUserResponse(UserListItem User, string? AccessToken, DateTimeOffset? ExpiresAt);

/// <summary>
/// User accounts. Unlike <c>Organization</c>/<c>Request</c>, <c>AppUser</c> has no domain aggregate
/// of its own (see its own doc comment) - by design, so a password hash is never at risk of being
/// treated as part of a business aggregate - so the permission checks below live here rather than
/// behind a domain guard, throwing the same typed <see cref="DomainException"/> subclasses
/// <see cref="QuoteManager.Api.ErrorHandling.DomainExceptionHandler"/> already maps for every other
/// feature, rather than inventing a second error convention just for this one.
/// </summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users");

        group.MapGet("", GetUsersAsync);
        group.MapPost("", CreateUserAsync);
        group.MapPut("/{userId:guid}", UpdateUserAsync);
        group.MapPost("/{userId:guid}/reset-password", ResetPasswordAsync);
    }

    /// <summary>
    /// Admin sees every user; anyone else sees only their own row - the same "filter, don't 403"
    /// idiom <c>RequestEndpoints.IsVendorOnlyView</c> already applies to quote visibility, so one
    /// endpoint and one frontend page can serve both the admin table and a "my profile" view.
    /// </summary>
    private static async Task<PagedResult<UserListItem>> GetUsersAsync(
        [AsParameters] PagedListQuery query,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var actor = currentUser.ToActor();

        var baseQuery = db.Users.AsNoTracking()
            .Where(u => actor.IsAdmin || u.Id == actor.Id)
            .OrderBy(u => u.DisplayName);

        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery.Skip(query.Skip).Take(query.ResolvedPageSize).ToListAsync(cancellationToken);

        var organizationNames = await ResolveOrganizationNamesAsync(db, rows.Select(u => u.OrganizationId), cancellationToken);
        var items = rows.Select(u => ToListItem(u, organizationNames.GetValueOrDefault(u.OrganizationId ?? Guid.Empty))).ToList();

        return new PagedResult<UserListItem>(items, query.ResolvedPage, query.ResolvedPageSize, total);
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        IPasswordHasher<AppUser> passwordHasher,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!currentUser.ToActor().IsAdmin)
        {
            throw new UserActionNotPermittedException("create a user");
        }

        var trimmedEmail = body.Email.Trim();

        if (await db.Users.AsNoTracking().AnyAsync(u => u.Email == trimmedEmail, cancellationToken))
        {
            return DuplicateEmailProblem();
        }

        if (body.OrganizationId is { } newUserOrgId
            && !await db.Organizations.AsNoTracking().AnyAsync(o => o.Id == newUserOrgId, cancellationToken))
        {
            return UnknownOrganizationProblem();
        }

        if (!AppRoleExtensions.TryParseRoles(body.Roles, out var roles))
        {
            throw new InvalidOperationException("Roles should already be validated by CreateUserRequest.Validate().");
        }

        var user = new AppUser
        {
            Id = Guid.CreateVersion7(timeProvider.GetUtcNow()),
            Email = trimmedEmail,
            DisplayName = body.DisplayName.Trim(),
            Roles = roles,
            OrganizationId = body.OrganizationId,
            Address = NormalizeOptional(body.Address),
            Phone = NormalizeOptional(body.Phone),
            PasswordHash = string.Empty,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, body.Password);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEmail(ex))
        {
            return DuplicateEmailProblem();
        }

        var organizationName = await ResolveOrganizationNameAsync(db, user.OrganizationId, cancellationToken);
        return Results.Created($"/api/users/{user.Id}", ToListItem(user, organizationName));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid userId,
        UpdateUserRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TokenService tokenService,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var actor = currentUser.ToActor();
        var isSelf = actor.Id == userId;

        // A non-admin editing someone else gets the identical 404 whether that id exists or not -
        // the same reason GET /api/requests/{id}/quotes/{quoteId} refuses a competitor 404 rather
        // than 403: a caller with no visibility right to another user shouldn't be able to confirm
        // which ids are real ones by the status code alone.
        if (user is null || (!isSelf && !actor.IsAdmin))
        {
            return Results.NotFound();
        }

        if (!AppRoleExtensions.TryParseRoles(body.Roles, out var roles))
        {
            throw new InvalidOperationException("Roles should already be validated by UpdateUserRequest.Validate().");
        }

        if (!actor.IsAdmin && (roles != user.Roles || body.OrganizationId != user.OrganizationId))
        {
            throw new UserActionNotPermittedException("change your own roles or organization");
        }

        if (body.OrganizationId is { } newOrgId
            && !await db.Organizations.AsNoTracking().AnyAsync(o => o.Id == newOrgId, cancellationToken))
        {
            return UnknownOrganizationProblem();
        }

        var trimmedEmail = body.Email.Trim();
        if (await db.Users.AsNoTracking().AnyAsync(u => u.Id != userId && u.Email == trimmedEmail, cancellationToken))
        {
            return DuplicateEmailProblem();
        }

        user.Email = trimmedEmail;
        user.DisplayName = body.DisplayName.Trim();
        user.Address = NormalizeOptional(body.Address);
        user.Phone = NormalizeOptional(body.Phone);

        if (actor.IsAdmin)
        {
            user.Roles = roles;
            user.OrganizationId = body.OrganizationId;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEmail(ex))
        {
            return DuplicateEmailProblem();
        }

        var organizationName = await ResolveOrganizationNameAsync(db, user.OrganizationId, cancellationToken);

        string? accessToken = null;
        DateTimeOffset? expiresAt = null;
        if (isSelf)
        {
            var issued = tokenService.IssueFor(user);
            accessToken = issued.AccessToken;
            expiresAt = issued.ExpiresAt;
        }

        return Results.Ok(new UpdateUserResponse(ToListItem(user, organizationName), accessToken, expiresAt));
    }

    private static async Task<IResult> ResetPasswordAsync(
        Guid userId,
        ResetPasswordRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        IPasswordHasher<AppUser> passwordHasher,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var actor = currentUser.ToActor();
        var isSelf = actor.Id == userId;

        if (user is null || (!isSelf && !actor.IsAdmin))
        {
            return Results.NotFound();
        }

        // Self-service requires proving you know the current password; an admin resetting someone
        // else's password has no way to supply it and doesn't need to - admin authority substitutes.
        if (isSelf)
        {
            if (body.CurrentPassword is null
                || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, body.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                throw new InvalidCurrentPasswordException();
            }
        }

        user.PasswordHash = passwordHasher.HashPassword(user, body.NewPassword);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static UserListItem ToListItem(AppUser user, string? organizationName) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Roles.Names(),
        user.OrganizationId,
        organizationName,
        user.Address,
        user.Phone);

    private static async Task<Dictionary<Guid, string>> ResolveOrganizationNamesAsync(
        QuoteManagerDbContext db, IEnumerable<Guid?> organizationIds, CancellationToken cancellationToken)
    {
        var ids = organizationIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        return await db.Organizations.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);
    }

    private static async Task<string?> ResolveOrganizationNameAsync(
        QuoteManagerDbContext db, Guid? organizationId, CancellationToken cancellationToken) =>
        organizationId is { } id
            ? await db.Organizations.AsNoTracking().Where(o => o.Id == id).Select(o => o.Name).SingleOrDefaultAsync(cancellationToken)
            : null;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsDuplicateEmail(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            && message.Contains("Users", StringComparison.OrdinalIgnoreCase);
    }

    private static IResult DuplicateEmailProblem() => Results.Problem(
        title: "Email already in use",
        detail: "Another user already has this email address.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: new Dictionary<string, object?> { ["code"] = "user.duplicate_email" });

    private static IResult UnknownOrganizationProblem() => Results.Problem(
        title: "Unknown organization",
        detail: "organizationId must reference an existing organization.",
        statusCode: StatusCodes.Status400BadRequest,
        extensions: new Dictionary<string, object?> { ["code"] = "user.unknown_organization" });
}
