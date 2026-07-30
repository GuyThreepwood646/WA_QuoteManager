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
        endpoints.MapPost("/api/requests/{requestId:guid}/quotes", CreateQuoteAsync);

        var group = endpoints.MapGroup("/api/requests/{requestId:guid}/quotes/{quoteId:guid}");

        group.MapGet("", GetQuoteAsync);
        group.MapPut("", EditQuoteAsync);
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

        var quote = request.AddQuote(
            body.VendorOrganizationId,
            new Money(body.Amount, body.Currency),
            body.ExpiresAt,
            body.Notes,
            currentUser.ToActor(),
            timeProvider.GetUtcNow());

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

        request.ApplyQuoteAction(quoteId, body.Action, currentUser.ToActor(), timeProvider.GetUtcNow(), expectedVersion, body.Note);

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
