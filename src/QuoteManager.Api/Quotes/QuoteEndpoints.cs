using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using QuoteManager.Api.Models;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Common;
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

public static class QuoteEndpoints
{
    public static void MapQuoteEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Drafting a quote against a request. Same rule as the transition endpoint below -
        // Request.AddQuote's CanActForVendorOrganization check is the sole authority on whether
        // this caller may draft under the named vendor organization.
        endpoints.MapPost("/api/requests/{requestId:guid}/quotes", CreateQuoteAsync);

        var group = endpoints.MapGroup("/api/requests/{requestId:guid}/quotes/{quoteId:guid}");

        // Reads carry a weak ETag so a client can round-trip it as If-Match.
        group.MapGet("", GetQuoteAsync);

        // Business-field edits (amount/currency/expiry/notes), distinct from a status transition -
        // Request.EditQuote resolves QuoteAction.Edit through the same QuoteTransitions table, so
        // ownership and the Draft-only rule are enforced there, not here.
        group.MapPut("", EditQuoteAsync);

        // The one action-driven transition endpoint. No [Authorize(Roles = ...)] here or anywhere
        // near it - QuoteTransitions.Resolve, called inside ApplyQuoteAction, is the only
        // authority on whether the actor's roles permit this action.
        group.MapPost("/transitions", ApplyActionAsync);
    }

    private static async Task<IResult> CreateQuoteAsync(
        Guid requestId,
        CreateQuoteRequest body,
        HttpContext httpContext,
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

        // Throws RequestNotEditableException (request already awarded/cancelled) or
        // QuoteTransitionNotAllowedException (wrong vendor organization) - both are mapped by the
        // DomainExceptionHandler, so no try/catch belongs here.
        var quote = request.AddQuote(
            body.VendorOrganizationId,
            new Money(body.Amount, body.Currency),
            body.ExpiresAt,
            body.Notes,
            currentUser.ToActor(),
            timeProvider.GetUtcNow());

        // request was loaded, not context.Add()-ed, so EF's change tracker has no signal that a
        // brand-new child reached via AddQuote's navigation mutation is Added rather than an
        // already-existing row - its key is already set (UUIDv7, assigned in the constructor), so
        // without this it is tracked as Modified and SaveChanges issues an UPDATE that matches zero
        // rows. Explicit, not a workaround: every future write path that grows a tracked aggregate's
        // child collection needs the same line.
        db.Quotes.Add(quote);

        await db.SaveChangesAsync(cancellationToken);

        SetETag(httpContext, quote.Version);
        return Results.Created(
            $"/api/requests/{requestId}/quotes/{quote.Id}",
            ToResponse(quote, currentUser.ToActor()));
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
        return Results.Ok(ToResponse(quote, currentUser.ToActor()));
    }

    private static async Task<IResult> EditQuoteAsync(
        Guid requestId,
        Guid quoteId,
        EditQuoteRequest body,
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
                detail: "Edits must round-trip the ETag returned by GET, so a lost update cannot silently overwrite a concurrent change.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "quote.if_match_required" });
        }

        var request = await db.Requests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return Results.NotFound();
        }

        // Throws QuoteNotFoundInRequestException / QuoteTransitionNotAllowedException (wrong
        // owner, or the quote is past Draft) / QuoteConcurrencyException on refusal - the
        // DomainExceptionHandler maps every one of them, so no try/catch belongs here.
        request.EditQuote(
            quoteId,
            new Money(body.Amount, body.Currency),
            body.ExpiresAt,
            body.Notes,
            currentUser.ToActor(),
            timeProvider.GetUtcNow(),
            expectedVersion);

        await db.SaveChangesAsync(cancellationToken);

        var quote = request.Quotes.First(q => q.Id == quoteId);
        SetETag(httpContext, quote.Version);
        return Results.Ok(ToResponse(quote, currentUser.ToActor()));
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
        // DomainExceptionHandler maps every one of them, so no try/catch belongs here.
        request.ApplyQuoteAction(quoteId, body.Action, currentUser.ToActor(), timeProvider.GetUtcNow(), expectedVersion);

        await db.SaveChangesAsync(cancellationToken);

        var quote = request.Quotes.First(q => q.Id == quoteId);
        SetETag(httpContext, quote.Version);
        return Results.Ok(ToResponse(quote, currentUser.ToActor()));
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

    private static QuoteResponse ToResponse(Quote quote, DomainActor actor) => new(
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
        QuoteTransitions.PermittedFor(quote.Status, actor, quote.VendorOrganizationId)
            .Select(action => action.ToString())
            .ToList());
}
