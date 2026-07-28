namespace QuoteManager.Infrastructure.Persistence.Entities;

/// <summary>
/// One recorded fact about something a user did.
/// </summary>
/// <remarks>
/// AD-5: written in the same transaction as the change it describes, projected from the same
/// domain events, so audit cannot be skipped by a code path that forgot to log. This is a
/// persistence-side record rather than a domain concept, which is why it lives in Infrastructure
/// and the Domain knows nothing about it.
/// </remarks>
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

    /// <summary>
    /// Correlates the audit row with the diagnostic trace for the same request.
    /// </summary>
    /// <remarks>
    /// Audit and logging stay separate sources of truth per AD-5; this is the join key that lets
    /// an investigator move between them without making either depend on the other.
    /// </remarks>
    public string? TraceId { get; init; }
}
