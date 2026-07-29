using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Common;
using QuoteManager.Api.Models;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Organizations;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Requests;

public sealed record RequestListItem(
    Guid Id,
    string Title,
    string ClientOrganizationName,
    string Status,
    int QuoteCount,
    DateTimeOffset? NeededBy,
    DateTimeOffset CreatedAt);

public sealed record RequestQuoteItem(
    Guid Id,
    Guid VendorOrganizationId,
    string VendorOrganizationName,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset? ExpiresAt,
    string? Notes,
    DateTimeOffset StatusChangedAt,
    string? StatusReason,
    int Version,
    IReadOnlyList<string> PermittedActions);

public sealed record RequestInvitationItem(
    Guid VendorOrganizationId,
    string VendorOrganizationName,
    DateTimeOffset InvitedAt,
    bool HasQuoted);

public sealed record RequestDetailResponse(
    Guid Id,
    string Title,
    string? Description,
    Guid ClientOrganizationId,
    string ClientOrganizationName,
    string Status,
    DateTimeOffset? NeededBy,
    DateTimeOffset CreatedAt,
    bool IsEditable,
    bool CanAddQuote,
    IReadOnlyList<RequestQuoteItem> Quotes,
    IReadOnlyList<RequestInvitationItem> Invitations);

/// <summary>
/// FR-1/FR-2: browsing and drilling into requests. The list is intentionally thin (AD-11 - no
/// aggregate fields the list screen does not use); the detail response is where a user actually
/// acts, via the quote transition endpoint and each quote's <c>permittedActions</c> (AD-7).
/// </summary>
public static class RequestEndpoints
{
    public static void MapRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/requests", GetRequestsAsync);
        endpoints.MapPost("/api/requests", CreateRequestAsync);
        endpoints.MapGet("/api/requests/{requestId:guid}", GetRequestAsync);
    }

    private static async Task<PagedResult<RequestListItem>> GetRequestsAsync(
        [AsParameters] PagedListQuery query,
        QuoteManagerDbContext db,
        CancellationToken cancellationToken)
    {
        var baseQuery =
            from request in db.Requests.AsNoTracking()
            join client in db.Organizations.AsNoTracking() on request.ClientOrganizationId equals client.Id
            orderby request.CreatedAt descending
            select new
            {
                request.Id,
                request.Title,
                ClientOrganizationName = client.Name,
                request.Status,
                QuoteCount = request.Quotes.Count,
                request.NeededBy,
                request.CreatedAt,
            };

        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery.Skip(query.Skip).Take(query.ResolvedPageSize).ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new RequestListItem(
                row.Id,
                row.Title,
                row.ClientOrganizationName,
                row.Status.ToString(),
                row.QuoteCount,
                row.NeededBy,
                row.CreatedAt))
            .ToList();

        return new PagedResult<RequestListItem>(items, query.ResolvedPage, query.ResolvedPageSize, total);
    }

    /// <summary>
    /// FR-1's other half: raising a request. <c>Request.Create</c> is the sole authority on
    /// whether the actor's role permits it (AD-13's client-side mirror), so a role check here
    /// would only be a second, driftable copy of that rule.
    /// </summary>
    private static async Task<IResult> CreateRequestAsync(
        CreateRequestRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var clientOrganization = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == body.ClientOrganizationId, cancellationToken);

        if (clientOrganization is null || clientOrganization.Kind != OrganizationKind.Client)
        {
            return Results.Problem(
                title: "Unknown client organisation",
                detail: "clientOrganizationId must reference an existing client organisation.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "request.unknown_client_organization" });
        }

        // Throws RequestCreationNotPermittedException for a Vendor/Reviewer caller; the
        // DomainExceptionHandler maps it to 403 (AD-8), so no role check belongs here.
        var request = Request.Create(
            body.Title,
            body.Description,
            body.ClientOrganizationId,
            body.NeededBy,
            currentUser.ToActor(),
            timeProvider.GetUtcNow());

        db.Requests.Add(request);
        await db.SaveChangesAsync(cancellationToken);

        var response = new RequestDetailResponse(
            request.Id,
            request.Title,
            request.Description,
            request.ClientOrganizationId,
            clientOrganization.Name,
            request.Status.ToString(),
            request.NeededBy,
            request.CreatedAt,
            request.IsEditable,
            ComputeCanAddQuote(request.IsEditable, currentUser.ToActor(), quotedVendorOrgIds: new HashSet<Guid>()),
            [],
            []);

        return Results.Created($"/api/requests/{request.Id}", response);
    }

    private static async Task<IResult> GetRequestAsync(
        Guid requestId,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // AutoInclude (RequestConfiguration) brings Quotes and Invitations along for free; there is
        // no navigation to Organization, so vendor/client names are resolved by a second lookup.
        var request = await db.Requests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            return Results.NotFound();
        }

        var actor = currentUser.ToActor();

        // Read-side half of AD-13: Admin/Reviewer/Requester are the client side of a request and
        // need every quote to compare offers, so they see everything. A pure Vendor account acts on
        // only its own organisation's quote (write side, already enforced by QuoteTransitions) and
        // must not be able to read a competitor's amount, notes, or even the fact that a competing
        // quote or invitation exists on a shared request - visibility is filtered to match.
        var visibleQuotes = IsVendorOnlyView(actor)
            ? request.Quotes.Where(quote => quote.VendorOrganizationId == actor.OrganizationId).ToList()
            : request.Quotes;
        var visibleInvitations = IsVendorOnlyView(actor)
            ? request.Invitations.Where(i => i.VendorOrganizationId == actor.OrganizationId).ToList()
            : request.Invitations;

        var organizationIds = visibleInvitations.Select(i => i.VendorOrganizationId)
            .Concat(visibleQuotes.Select(q => q.VendorOrganizationId))
            .Append(request.ClientOrganizationId)
            .Distinct()
            .ToList();

        var organizationNames = await db.Organizations.AsNoTracking()
            .Where(o => organizationIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        // HasQuoted is computed against every quote, not just the visible ones - it is a boolean
        // the viewer is entitled to know about their own invitation regardless of who else quoted.
        var quotedVendorIds = request.Quotes.Select(q => q.VendorOrganizationId).ToHashSet();

        var response = new RequestDetailResponse(
            request.Id,
            request.Title,
            request.Description,
            request.ClientOrganizationId,
            organizationNames.GetValueOrDefault(request.ClientOrganizationId, "Unknown"),
            request.Status.ToString(),
            request.NeededBy,
            request.CreatedAt,
            request.IsEditable,
            ComputeCanAddQuote(request.IsEditable, actor, quotedVendorIds),
            visibleQuotes.Select(quote => new RequestQuoteItem(
                quote.Id,
                quote.VendorOrganizationId,
                organizationNames.GetValueOrDefault(quote.VendorOrganizationId, "Unknown"),
                quote.Status.ToString(),
                quote.Amount.Amount,
                quote.Amount.CurrencyCode,
                quote.ExpiresAt,
                quote.Notes,
                quote.StatusChangedAt,
                quote.StatusReason,
                quote.Version,
                QuoteTransitions.PermittedFor(quote.Status, actor, quote.VendorOrganizationId)
                    .Select(a => a.ToString())
                    .ToList()))
                .ToList(),
            visibleInvitations.Select(invitation => new RequestInvitationItem(
                invitation.VendorOrganizationId,
                organizationNames.GetValueOrDefault(invitation.VendorOrganizationId, "Unknown"),
                invitation.InvitedAt,
                quotedVendorIds.Contains(invitation.VendorOrganizationId)))
                .ToList());

        return Results.Ok(response);
    }

    /// <summary>Internal rather than private: shared with <see cref="RequestActivityEndpoints"/>, which applies the exact same read-side filter to the timeline.</summary>
    internal static bool IsVendorOnlyView(DomainActor actor) =>
        actor.Roles.HasAny(AppRole.Vendor) && !actor.Roles.HasAny(AppRole.Admin | AppRole.Reviewer | AppRole.Requester);

    /// <summary>
    /// AD-7's request-level counterpart to a quote's <c>permittedActions</c>: whether this viewer
    /// should be shown a form to draft a new quote on this request. Scoped to the vendor
    /// self-serve path only - Admin can still call the create-quote endpoint on behalf of any
    /// vendor, but that is a support action with no dedicated screen, not a signal this field
    /// needs to carry.
    /// </summary>
    private static bool ComputeCanAddQuote(bool isEditable, DomainActor actor, HashSet<Guid> quotedVendorOrgIds) =>
        isEditable
        && actor.Roles.HasAny(AppRole.Vendor)
        && actor.OrganizationId is { } organizationId
        && !quotedVendorOrgIds.Contains(organizationId);
}
