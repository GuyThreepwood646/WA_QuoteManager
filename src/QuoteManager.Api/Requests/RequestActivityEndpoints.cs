using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Common;
using QuoteManager.Api.Models;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Requests;

public sealed record ActivityEntryResponse(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    string Action,
    string Summary,
    string ActorDisplayName,
    DateTimeOffset OccurredAt);

/// <summary>
/// FR-4/FR-5: the per-request activity timeline AD-5 names as the thing that reads
/// <c>AuditEntry</c> directly - "what happened", laid out in one place instead of scattered across
/// each screen re-deriving it from the entities it currently touches.
/// </summary>
public static class RequestActivityEndpoints
{
    public static void MapRequestActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/requests/{requestId:guid}/activity", GetActivityAsync);
    }

    private static async Task<IResult> GetActivityAsync(
        Guid requestId,
        [AsParameters] PagedListQuery query,
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

        var actor = currentUser.ToActor();

        // Read-side half of AD-13, mirroring RequestEndpoints.GetRequestAsync exactly: a pure
        // Vendor viewer must not learn a competitor's quote history - draft amounts, edits,
        // withdrawals - through the timeline any more than through the quotes list itself. Every
        // event whose SubjectType is Request (RequestCreated, VendorInvited, RequestAwarded,
        // RequestCancelled) names no vendor and carries no money in its Summary, so it is always
        // visible to anyone who can see the request at all; only Quote-subject rows need filtering.
        var visibleQuoteIds = RequestEndpoints.IsVendorOnlyView(actor)
            ? request.Quotes.Where(q => q.VendorOrganizationId == actor.OrganizationId).Select(q => q.Id).ToHashSet()
            : request.Quotes.Select(q => q.Id).ToHashSet();

        var baseQuery = db.AuditEntries.AsNoTracking()
            .Where(e =>
                (e.SubjectType == nameof(Request) && e.SubjectId == requestId) ||
                (e.SubjectType == nameof(Quote) && visibleQuoteIds.Contains(e.SubjectId)));

        var total = await baseQuery.CountAsync(cancellationToken);

        // Newest first, per the list-query convention's default sort direction; Id (UUIDv7) breaks
        // ties deterministically when two entries share the exact same second-precision timestamp,
        // as several seeded actions on the same request do.
        var rows = await baseQuery
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip(query.Skip)
            .Take(query.ResolvedPageSize)
            .Select(e => new ActivityEntryResponse(
                e.Id,
                e.SubjectType,
                e.SubjectId,
                e.Action,
                e.Summary,
                e.ActorDisplayName,
                e.OccurredAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResult<ActivityEntryResponse>(rows, query.ResolvedPage, query.ResolvedPageSize, total));
    }
}
