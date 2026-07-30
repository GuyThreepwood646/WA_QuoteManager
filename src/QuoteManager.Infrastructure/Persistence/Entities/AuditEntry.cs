namespace QuoteManager.Infrastructure.Persistence.Entities;

/// <summary>
/// One recorded fact about something a user did — a persistence-side projection of domain events
/// (AD-5), not a domain concept, which is why it lives in Infrastructure.
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; init; }

    /// <summary>Entity kind the entry is about, addressed polymorphically with <see cref="SubjectId"/>.</summary>
    public required string SubjectType { get; init; }

    public Guid SubjectId { get; init; }

    /// <summary>Stable machine name of what happened, such as <c>QuoteAccepted</c>.</summary>
    public required string Action { get; init; }

    public required string Summary { get; init; }

    public Guid ActorId { get; init; }

    public required string ActorDisplayName { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Free-text note the actor typed for this event, if any (e.g. why a quote was rejected).</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Correlates the audit row with the diagnostic trace for the same request — audit and logging
    /// stay separate sources of truth (AD-5); this is just the join key between them.
    /// </summary>
    public string? TraceId { get; init; }
}
