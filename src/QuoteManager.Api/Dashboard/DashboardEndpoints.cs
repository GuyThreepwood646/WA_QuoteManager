using Microsoft.EntityFrameworkCore;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Dashboard;

public sealed record QuoteTriageItem(
    Guid QuoteId,
    Guid RequestId,
    string RequestTitle,
    string VendorOrganizationName,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset StatusChangedAt,
    int Version,
    IReadOnlyList<string> PermittedActions);

public sealed record RequestAwaitingResponseItem(
    Guid RequestId,
    string Title,
    string ClientOrganizationName,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> AwaitingVendorNames);

public sealed record DashboardResponse(
    IReadOnlyList<QuoteTriageItem> QuotesNeedingReview,
    IReadOnlyList<QuoteTriageItem> QuotesUnderReview,
    IReadOnlyList<QuoteTriageItem> QuotesExpiringSoon,
    IReadOnlyList<RequestAwaitingResponseItem> RequestsAwaitingResponse);

/// <summary>
/// FR-4: "see what's happening, focus on the right work" - a triage/prioritisation surface, not a
/// CRUD grid. Every bucket here answers a specific question a user actually has, rather than being
/// a filtered view of one big list.
/// </summary>
public static class DashboardEndpoints
{
    /// <summary>How close to expiry counts as "soon" for the triage view.</summary>
    private static readonly TimeSpan ExpirySoonWindow = TimeSpan.FromDays(3);

    public static void MapDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dashboard", GetDashboardAsync);
    }

    private static async Task<DashboardResponse> GetDashboardAsync(
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var actor = currentUser.ToActor();

        // AD-11: one AsNoTracking projection joining Quotes/Requests/Organizations by id (there is
        // no navigation property between them - see AD-3's note on why Quote carries no
        // VendorOrganization reference). permittedActions is computed after materialising, since
        // QuoteTransitions.PermittedFor is plain C# and cannot translate to SQL.
        var quoteRows = await (
            from quote in db.Quotes.AsNoTracking()
            join request in db.Requests.AsNoTracking() on quote.RequestId equals request.Id
            join vendor in db.Organizations.AsNoTracking() on quote.VendorOrganizationId equals vendor.Id
            where quote.Status == QuoteStatus.Submitted || quote.Status == QuoteStatus.UnderReview
            select new
            {
                Quote = quote,
                RequestTitle = request.Title,
                VendorName = vendor.Name,
            }).ToListAsync(cancellationToken);

        var triageItems = quoteRows
            .Select(row => new QuoteTriageItem(
                row.Quote.Id,
                row.Quote.RequestId,
                row.RequestTitle,
                row.VendorName,
                row.Quote.Amount.Amount,
                row.Quote.Amount.CurrencyCode,
                row.Quote.Status.ToString(),
                row.Quote.ExpiresAt,
                row.Quote.StatusChangedAt,
                row.Quote.Version,
                QuoteTransitions.PermittedFor(row.Quote.Status, actor, row.Quote.VendorOrganizationId)
                    .Select(a => a.ToString())
                    .ToList()))
            .ToList();

        var expiryThreshold = now.Add(ExpirySoonWindow);

        var response = new DashboardResponse(
            QuotesNeedingReview: triageItems
                .Where(item => item.Status == nameof(QuoteStatus.Submitted))
                .OrderBy(item => item.StatusChangedAt)
                .ToList(),
            QuotesUnderReview: triageItems
                .Where(item => item.Status == nameof(QuoteStatus.UnderReview))
                .OrderBy(item => item.StatusChangedAt)
                .ToList(),
            QuotesExpiringSoon: triageItems
                .Where(item => item.ExpiresAt is { } expiresAt && expiresAt <= expiryThreshold)
                .OrderBy(item => item.ExpiresAt)
                .ToList(),
            RequestsAwaitingResponse: await GetRequestsAwaitingResponseAsync(db, cancellationToken));

        return response;
    }

    private static async Task<IReadOnlyList<RequestAwaitingResponseItem>> GetRequestsAwaitingResponseAsync(
        QuoteManagerDbContext db,
        CancellationToken cancellationToken)
    {
        var silentInvitations = await (
            from invitation in db.RequestInvitations.AsNoTracking()
            join request in db.Requests.AsNoTracking() on invitation.RequestId equals request.Id
            join vendor in db.Organizations.AsNoTracking() on invitation.VendorOrganizationId equals vendor.Id
            where request.Status == RequestStatus.Open
                  && !db.Quotes.Any(q =>
                      q.RequestId == invitation.RequestId && q.VendorOrganizationId == invitation.VendorOrganizationId)
            select new
            {
                request.Id,
                request.Title,
                request.ClientOrganizationId,
                request.CreatedAt,
                VendorName = vendor.Name,
            }).ToListAsync(cancellationToken);

        if (silentInvitations.Count == 0)
        {
            return [];
        }

        var clientOrgNames = await db.Organizations.AsNoTracking()
            .Where(o => silentInvitations.Select(i => i.ClientOrganizationId).Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

        return silentInvitations
            .GroupBy(row => new { row.Id, row.Title, row.ClientOrganizationId, row.CreatedAt })
            .Select(group => new RequestAwaitingResponseItem(
                group.Key.Id,
                group.Key.Title,
                clientOrgNames.GetValueOrDefault(group.Key.ClientOrganizationId, "Unknown"),
                group.Key.CreatedAt,
                group.Select(row => row.VendorName).ToList()))
            .OrderBy(item => item.CreatedAt)
            .ToList();
    }
}
