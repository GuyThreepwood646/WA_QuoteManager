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
    bool CanEdit,
    bool CanCancel,
    bool CanInviteVendor,
    IReadOnlyList<RequestQuoteItem> Quotes,
    IReadOnlyList<RequestInvitationItem> Invitations);

public static class RequestEndpoints
{
    public static void MapRequestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/requests", GetRequestsAsync);
        endpoints.MapPost("/api/requests", CreateRequestAsync);
        endpoints.MapGet("/api/requests/{requestId:guid}", GetRequestAsync);
        endpoints.MapPut("/api/requests/{requestId:guid}", UpdateRequestAsync);
        endpoints.MapPost("/api/requests/{requestId:guid}/cancel", CancelRequestAsync);
        endpoints.MapPost("/api/requests/{requestId:guid}/invitations", InviteVendorAsync);
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
                title: "Unknown client organization",
                detail: "clientOrganizationId must reference an existing client organization.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "request.unknown_client_organization" });
        }

        var request = Request.Create(
            body.Title,
            body.Description,
            body.ClientOrganizationId,
            body.NeededBy,
            currentUser.ToActor(),
            timeProvider.GetUtcNow());

        db.Requests.Add(request);
        await db.SaveChangesAsync(cancellationToken);

        var response = await BuildDetailResponseAsync(request, currentUser.ToActor(), db, cancellationToken);
        return Results.Created($"/api/requests/{request.Id}", response);
    }

    private static async Task<IResult> GetRequestAsync(
        Guid requestId,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var request = await db.Requests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request is null)
        {
            return Results.NotFound();
        }

        var response = await BuildDetailResponseAsync(request, currentUser.ToActor(), db, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> UpdateRequestAsync(
        Guid requestId,
        UpdateRequestRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        var actor = currentUser.ToActor();
        request.Update(body.Title, body.Description, body.NeededBy, actor, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        var response = await BuildDetailResponseAsync(request, actor, db, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CancelRequestAsync(
        Guid requestId,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        var actor = currentUser.ToActor();
        request.Cancel(actor, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        var response = await BuildDetailResponseAsync(request, actor, db, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> InviteVendorAsync(
        Guid requestId,
        InviteVendorRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        var vendorOrganization = await db.Organizations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == body.VendorOrganizationId, cancellationToken);

        if (vendorOrganization is null || vendorOrganization.Kind != OrganizationKind.Vendor)
        {
            return Results.Problem(
                title: "Unknown vendor organization",
                detail: "vendorOrganizationId must reference an existing vendor organization.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "request.unknown_vendor_organization" });
        }

        var actor = currentUser.ToActor();
        request.InviteVendor(body.VendorOrganizationId, actor, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        var response = await BuildDetailResponseAsync(request, actor, db, cancellationToken);
        return Results.Ok(response);
    }

    /// <summary>Internal rather than private: shared with <see cref="RequestActivityEndpoints"/>, which applies the exact same read-side filter to the timeline.</summary>
    internal static bool IsVendorOnlyView(DomainActor actor) =>
        actor.Roles.HasAny(AppRole.Vendor) && !actor.Roles.HasAny(AppRole.Admin | AppRole.Reviewer | AppRole.Requester);

    private static async Task<RequestDetailResponse> BuildDetailResponseAsync(
        Request request,
        DomainActor actor,
        QuoteManagerDbContext db,
        CancellationToken cancellationToken)
    {
        // AD-13: a pure Vendor account must not see a competitor's quote or invitation on a shared
        // request, so only its own view is filtered here; Admin/Reviewer/Requester need every quote.
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

        // HasQuoted ignores inactive quotes (Withdrawn/Expired/Rejected) — those vendors may revise or draft anew.
        var quotedVendorIds = VendorsWithActiveQuotes(request.Quotes);

        var canActOnRequest = actor.Roles.HasAny(AppRole.Requester | AppRole.Admin);

        return new RequestDetailResponse(
            request.Id,
            request.Title,
            request.Description,
            request.ClientOrganizationId,
            organizationNames.GetValueOrDefault(request.ClientOrganizationId, "Unknown"),
            request.Status.ToString(),
            request.NeededBy,
            request.CreatedAt,
            request.IsEditable,
            ComputeCanAddQuote(request.Status == RequestStatus.Open, actor, quotedVendorIds),
            request.IsEditable && canActOnRequest,
            request.Status == RequestStatus.Open && canActOnRequest,
            request.Status == RequestStatus.Open && canActOnRequest,
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
                request.Status == RequestStatus.Open
                    ? QuoteTransitions.PermittedFor(quote.Status, actor, quote.VendorOrganizationId)
                        .Select(a => a.ToString())
                        .ToList()
                    : []))
                .ToList(),
            visibleInvitations.Select(invitation => new RequestInvitationItem(
                invitation.VendorOrganizationId,
                organizationNames.GetValueOrDefault(invitation.VendorOrganizationId, "Unknown"),
                invitation.InvitedAt,
                quotedVendorIds.Contains(invitation.VendorOrganizationId)))
                .ToList());
    }

    private static HashSet<Guid> VendorsWithActiveQuotes(IEnumerable<Quote> quotes) =>
        quotes.Where(q => !QuoteTransitions.IsInactive(q.Status))
            .Select(q => q.VendorOrganizationId)
            .ToHashSet();

    /// <summary>
    /// Vendor self-serve: own org, and only if it has not quoted yet. Admin: any open request —
    /// they pick the vendor on the form, matching <c>Request.AddQuote</c>.
    /// </summary>
    private static bool ComputeCanAddQuote(bool isOpen, DomainActor actor, HashSet<Guid> quotedVendorOrgIds)
    {
        if (!isOpen)
        {
            return false;
        }

        if (actor.Roles.HasAny(AppRole.Admin))
        {
            return true;
        }

        return actor.Roles.HasAny(AppRole.Vendor)
            && actor.OrganizationId is { } organizationId
            && !quotedVendorOrgIds.Contains(organizationId);
    }
}
