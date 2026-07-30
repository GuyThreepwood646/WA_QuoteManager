using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Requests;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Dashboard;

public sealed record DashboardQuoteItem(
    Guid QuoteId,
    Guid VendorOrganizationId,
    string VendorOrganizationName,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset StatusChangedAt,
    int Version,
    bool IsExpiringSoon,
    IReadOnlyList<string> PermittedActions);

public sealed record DashboardRequestItem(
    Guid RequestId,
    string Title,
    string ClientOrganizationName,
    DateTimeOffset? NeededBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DashboardQuoteItem> Quotes,
    IReadOnlyList<string> AwaitingVendorNames);

/// <summary>Platform-wide counters, not scoped to the "needs attention" set below (see <see cref="DashboardEndpoints"/>).</summary>
public sealed record DashboardKpis(
    int OpenRequestCount,
    int QuotesAwaitingDecisionCount,
    int RequestsOpenedThisMonth,
    int RequestsClosedThisMonth,
    double? VendorResponseRatePercent);

public sealed record DashboardResponse(
    DashboardKpis Kpis,
    IReadOnlyList<DashboardRequestItem> Requests);

/// <summary>
/// A request-centric triage surface: one card per request that needs attention, with every quote
/// on it shown as a sub-row - so a request with three competing vendor quotes in three different
/// states reads as one thing, not three unrelated entries scattered across separate buckets.
/// </summary>
public static class DashboardEndpoints
{
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
        var isVendorOnlyView = RequestEndpoints.IsVendorOnlyView(actor);

        var requestItems = await BuildRequestItemsAsync(db, actor, isVendorOnlyView, now, cancellationToken);
        var kpis = await BuildKpisAsync(db, actor, isVendorOnlyView, now, cancellationToken);

        return new DashboardResponse(kpis, requestItems);
    }

    private static async Task<IReadOnlyList<DashboardRequestItem>> BuildRequestItemsAsync(
        QuoteManagerDbContext db,
        DomainActor actor,
        bool isVendorOnlyView,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeQuoteRows = await (
            from quote in db.Quotes.AsNoTracking()
            join request in db.Requests.AsNoTracking() on quote.RequestId equals request.Id
            where request.Status == RequestStatus.Open
                  && (quote.Status == QuoteStatus.Submitted || quote.Status == QuoteStatus.UnderReview)
                  && (!isVendorOnlyView || quote.VendorOrganizationId == actor.OrganizationId)
            select new { quote.RequestId }).ToListAsync(cancellationToken);
        var activeQuoteRequestIds = activeQuoteRows.Select(row => row.RequestId);

        var silentInvitationRows = await (
            from invitation in db.RequestInvitations.AsNoTracking()
            join request in db.Requests.AsNoTracking() on invitation.RequestId equals request.Id
            join vendor in db.Organizations.AsNoTracking() on invitation.VendorOrganizationId equals vendor.Id
            where request.Status == RequestStatus.Open
                  && !db.Quotes.Any(q => q.RequestId == invitation.RequestId && q.VendorOrganizationId == invitation.VendorOrganizationId)
                  && (!isVendorOnlyView || invitation.VendorOrganizationId == actor.OrganizationId)
            select new { invitation.RequestId, VendorName = vendor.Name }).ToListAsync(cancellationToken);

        var qualifyingIds = activeQuoteRequestIds
            .Concat(silentInvitationRows.Select(row => row.RequestId))
            .Distinct()
            .ToList();

        if (qualifyingIds.Count == 0)
        {
            return [];
        }

        var requestRows = await (
            from request in db.Requests.AsNoTracking()
            join client in db.Organizations.AsNoTracking() on request.ClientOrganizationId equals client.Id
            where qualifyingIds.Contains(request.Id)
            select new
            {
                request.Id,
                request.Title,
                ClientOrganizationName = client.Name,
                request.NeededBy,
                request.CreatedAt,
            }).ToListAsync(cancellationToken);

        // Every quote regardless of status - the whole point of the redesign is showing the full
        // competitive picture for a request, not just the one quote that triggered inclusion above.
        var quoteRows = await (
            from quote in db.Quotes.AsNoTracking()
            join vendor in db.Organizations.AsNoTracking() on quote.VendorOrganizationId equals vendor.Id
            where qualifyingIds.Contains(quote.RequestId)
                  && (!isVendorOnlyView || quote.VendorOrganizationId == actor.OrganizationId)
            select new { Quote = quote, VendorName = vendor.Name }).ToListAsync(cancellationToken);

        var expiryThreshold = now.Add(ExpirySoonWindow);
        var quotesByRequest = quoteRows
            .GroupBy(row => row.Quote.RequestId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DashboardQuoteItem>)group.Select(row => ToQuoteItem(row.Quote, row.VendorName, actor, expiryThreshold)).ToList());

        var awaitingByRequest = silentInvitationRows
            .GroupBy(row => row.RequestId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(row => row.VendorName).ToList());

        var items = requestRows.Select(row => new DashboardRequestItem(
            row.Id,
            row.Title,
            row.ClientOrganizationName,
            row.NeededBy,
            row.CreatedAt,
            quotesByRequest.GetValueOrDefault(row.Id, []),
            awaitingByRequest.GetValueOrDefault(row.Id, []))).ToList();

        return items
            .OrderBy(item => NearestDeadline(item))
            .ThenBy(item => item.CreatedAt)
            .ToList();
    }

    private static DashboardQuoteItem ToQuoteItem(Quote quote, string vendorName, DomainActor actor, DateTimeOffset expiryThreshold) =>
        new(
            quote.Id,
            quote.VendorOrganizationId,
            vendorName,
            quote.Amount.Amount,
            quote.Amount.CurrencyCode,
            quote.Status.ToString(),
            quote.ExpiresAt,
            quote.StatusChangedAt,
            quote.Version,
            IsExpiringSoon: (quote.Status == QuoteStatus.Submitted || quote.Status == QuoteStatus.UnderReview)
                && quote.ExpiresAt is { } expiresAt && expiresAt <= expiryThreshold,
            QuoteTransitions.PermittedFor(quote.Status, actor, quote.VendorOrganizationId)
                .Select(a => a.ToString())
                .ToList());

    /// <summary>
    /// The soonest of the request's own deadline and any of its still-active quotes' expiry - a
    /// resolved (terminal) quote's <c>ExpiresAt</c> no longer represents anything actionable, so it
    /// is excluded. A request with no deadline anywhere sorts last.
    /// </summary>
    private static DateTimeOffset NearestDeadline(DashboardRequestItem item)
    {
        var candidates = item.Quotes
            .Where(quote => quote.Status is nameof(QuoteStatus.Draft) or nameof(QuoteStatus.Submitted) or nameof(QuoteStatus.UnderReview))
            .Select(quote => quote.ExpiresAt)
            .Where(expiresAt => expiresAt is not null)
            .Select(expiresAt => expiresAt!.Value);

        if (item.NeededBy is { } neededBy)
        {
            candidates = candidates.Append(neededBy);
        }

        return candidates.DefaultIfEmpty(DateTimeOffset.MaxValue).Min();
    }

    private static async Task<DashboardKpis> BuildKpisAsync(
        QuoteManagerDbContext db,
        DomainActor actor,
        bool isVendorOnlyView,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var openRequestCount = await db.Requests.AsNoTracking()
            .CountAsync(r => r.Status == RequestStatus.Open, cancellationToken);

        var quotesAwaitingDecisionCount = await db.Quotes.AsNoTracking()
            .Where(q => q.Status == QuoteStatus.Submitted || q.Status == QuoteStatus.UnderReview)
            .Where(q => !isVendorOnlyView || q.VendorOrganizationId == actor.OrganizationId)
            .CountAsync(cancellationToken);

        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);

        var requestsOpenedThisMonth = await db.Requests.AsNoTracking()
            .CountAsync(r => r.CreatedAt >= monthStart && r.CreatedAt < monthEnd, cancellationToken);

        var requestsClosedThisMonth = await db.AuditEntries.AsNoTracking()
            .CountAsync(a => a.SubjectType == nameof(Request)
                && (a.Action == nameof(RequestAwarded) || a.Action == nameof(RequestCancelled))
                && a.OccurredAt >= monthStart && a.OccurredAt < monthEnd, cancellationToken);

        double? vendorResponseRatePercent = null;
        if (!isVendorOnlyView)
        {
            var totalInvitations = await db.RequestInvitations.AsNoTracking().CountAsync(cancellationToken);
            if (totalInvitations > 0)
            {
                var respondedInvitations = await db.RequestInvitations.AsNoTracking()
                    .CountAsync(invitation => db.Quotes.Any(q =>
                        q.RequestId == invitation.RequestId && q.VendorOrganizationId == invitation.VendorOrganizationId),
                        cancellationToken);

                vendorResponseRatePercent = 100.0 * respondedInvitations / totalInvitations;
            }
        }

        return new DashboardKpis(
            openRequestCount,
            quotesAwaitingDecisionCount,
            requestsOpenedThisMonth,
            requestsClosedThisMonth,
            vendorResponseRatePercent);
    }
}
