using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Identity;
using QuoteManager.Domain.Quotes;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Quotes;

public sealed record QuoteResponse(
    Guid Id,
    Guid RequestId,
    Guid VendorOrganizationId,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset? ExpiresAt,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset StatusChangedAt,
    string? StatusReason,
    int Version,
    IReadOnlyList<string> PermittedActions);

public sealed record ApplyQuoteActionRequest(QuoteAction Action);

public static class QuoteEndpoints
{
    public static void MapQuoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/requests/{requestId:guid}/quotes/{quoteId:guid}");

        // AD-15: reads carry a weak ETag so a client can round-trip it as If-Match.
        group.MapGet("", GetQuoteAsync);

        // AD-2: the one action-driven transition endpoint. No [Authorize(Roles = ...)] here or
        // anywhere near it - QuoteTransitions.Resolve, called inside ApplyQuoteAction, is the only
        // authority on whether the actor's roles permit this action.
        group.MapPost("/transitions", ApplyActionAsync);
    }

    private static async Task<IResult> GetQuoteAsync(
        Guid requestId,
        Guid quoteId,
        HttpContext httpContext,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var request = await db.Requests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        var quote = request?.Quotes.FirstOrDefault(q => q.Id == quoteId);
        if (quote is null)
        {
            return Results.NotFound();
        }

        SetETag(httpContext, quote.Version);
        return Results.Ok(ToResponse(quote, currentUser.Roles));
    }

    private static async Task<IResult> ApplyActionAsync(
        Guid requestId,
        Guid quoteId,
        ApplyQuoteActionRequest body,
        HttpContext httpContext,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!TryReadIfMatchVersion(httpContext.Request, out var expectedVersion))
        {
            return Results.Problem(
                title: "A valid If-Match header is required",
                detail: "Transitions must round-trip the ETag returned by GET, so a lost update cannot silently overwrite a concurrent change.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "quote.if_match_required" });
        }

        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        // Throws a typed QuoteNotFoundInRequestException / QuoteTransitionNotAllowedException /
        // QuoteAlreadyAcceptedException / QuoteConcurrencyException on refusal - the
        // DomainExceptionHandler maps every one of them, so no try/catch belongs here (AD-8).
        request.ApplyQuoteAction(quoteId, body.Action, currentUser.ToActor(), timeProvider.GetUtcNow(), expectedVersion);

        await db.SaveChangesAsync(cancellationToken);

        var quote = request.Quotes.First(q => q.Id == quoteId);
        SetETag(httpContext, quote.Version);
        return Results.Ok(ToResponse(quote, currentUser.Roles));
    }

    private static bool TryReadIfMatchVersion(HttpRequest request, out int version)
    {
        version = 0;
        var ifMatch = request.GetTypedHeaders().IfMatch;

        if (ifMatch is not { Count: 1 })
        {
            return false;
        }

        var tag = ifMatch[0].Tag.Value?.Trim('"');
        return int.TryParse(tag, out version);
    }

    private static void SetETag(HttpContext httpContext, int version) =>
        httpContext.Response.GetTypedHeaders().ETag = new EntityTagHeaderValue($"\"{version}\"", isWeak: true);

    private static QuoteResponse ToResponse(Quote quote, AppRole actorRoles) => new(
        quote.Id,
        quote.RequestId,
        quote.VendorOrganizationId,
        quote.Status.ToString(),
        quote.Amount.Amount,
        quote.Amount.CurrencyCode,
        quote.ExpiresAt,
        quote.Notes,
        quote.CreatedAt,
        quote.StatusChangedAt,
        quote.StatusReason,
        quote.Version,
        QuoteTransitions.PermittedFor(quote.Status, actorRoles).Select(action => action.ToString()).ToList());
}
