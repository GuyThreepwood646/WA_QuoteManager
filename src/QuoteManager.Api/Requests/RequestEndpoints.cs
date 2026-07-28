using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Common;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Identity;
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
        endpoints.MapGet("/api/requests/{requestId:guid}", GetRequestAsync);
    }

    private static async Task<PagedResult<RequestListItem>> GetRequestsAsync(
        int? page,
        int? pageSize,
        QuoteManagerDbContext db,
        CancellationToken cancellationToken)
    {
        var query = new PagedQuery(page, pageSize);

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
        var rows = await baseQuery.Skip(query.Skip).Take(query.PageSize).ToListAsync(cancellationToken);

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

        return new PagedResult<RequestListItem>(items, query.Page, query.PageSize, total);
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

        var organizationIds = request.Invitations.Select(i => i.VendorOrganizationId)
            .Concat(request.Quotes.Select(q => q.VendorOrganizationId))
            .Append(request.ClientOrganizationId)
            .Distinct()
            .ToList();

        var organizationNames = await db.Organizations.AsNoTracking()
            .Where(o => organizationIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        var roles = currentUser.Roles;
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
            request.Quotes.Select(quote => new RequestQuoteItem(
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
                QuoteTransitions.PermittedFor(quote.Status, roles).Select(a => a.ToString()).ToList()))
                .ToList(),
            request.Invitations.Select(invitation => new RequestInvitationItem(
                invitation.VendorOrganizationId,
                organizationNames.GetValueOrDefault(invitation.VendorOrganizationId, "Unknown"),
                invitation.InvitedAt,
                quotedVendorIds.Contains(invitation.VendorOrganizationId)))
                .ToList());

        return Results.Ok(response);
    }
}
