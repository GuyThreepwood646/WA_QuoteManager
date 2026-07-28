using QuoteManager.Domain.Common;
using QuoteManager.Domain.Quotes;

namespace QuoteManager.Domain.Requests;

public sealed record RequestCreated(
    Guid RequestId,
    string Title,
    Guid ClientOrganizationId,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Request);

    public Guid SubjectId => RequestId;

    public string Action => nameof(RequestCreated);

    public string Summary => $"Created request '{Title}'.";
}

public sealed record RequestUpdated(
    Guid RequestId,
    string Title,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Request);

    public Guid SubjectId => RequestId;

    public string Action => nameof(RequestUpdated);

    public string Summary => $"Updated request details for '{Title}'.";
}

public sealed record RequestAwarded(
    Guid RequestId,
    Guid AcceptedQuoteId,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Request);

    public Guid SubjectId => RequestId;

    public string Action => nameof(RequestAwarded);

    public string Summary => "Request awarded to the accepted quote.";
}

public sealed record RequestCancelled(
    Guid RequestId,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Request);

    public Guid SubjectId => RequestId;

    public string Action => nameof(RequestCancelled);

    public string Summary => "Request cancelled.";
}

public sealed record QuoteDrafted(
    Guid RequestId,
    Guid QuoteId,
    Guid VendorOrganizationId,
    Money Amount,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Quote);

    public Guid SubjectId => QuoteId;

    public string Action => nameof(QuoteDrafted);

    public string Summary => $"Drafted a quote for {Amount}.";
}

public sealed record QuoteEdited(
    Guid RequestId,
    Guid QuoteId,
    Money Amount,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Quote);

    public Guid SubjectId => QuoteId;

    public string Action => nameof(QuoteEdited);

    public string Summary => $"Amended the quote to {Amount}.";
}

/// <summary>
/// A quote moved between lifecycle states.
/// </summary>
/// <remarks>
/// One event type carrying the action rather than seven near-identical types. The audit
/// <see cref="Action"/> still resolves to a specific name such as <c>QuoteAccepted</c>, so the
/// timeline and the integration allow-list stay as expressive as they would be with separate
/// classes, without the transition table's contents being duplicated as a class hierarchy that
/// could drift from it.
/// </remarks>
public sealed record QuoteStatusChanged(
    Guid RequestId,
    Guid QuoteId,
    QuoteAction TriggeringAction,
    QuoteStatus From,
    QuoteStatus To,
    string? Reason,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Quote);

    public Guid SubjectId => QuoteId;

    public string Action => $"Quote{To}";

    public string Summary => Reason is null
        ? $"Quote moved from {From} to {To}."
        : $"Quote moved from {From} to {To} ({Reason}).";
}
