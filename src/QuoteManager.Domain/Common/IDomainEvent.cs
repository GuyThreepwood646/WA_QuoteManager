namespace QuoteManager.Domain.Common;

/// <summary>
/// A fact about something that already happened inside an aggregate.
/// </summary>
/// <remarks>
/// Per AD-4 and AD-5, every domain event is written to the audit trail, while only
/// events on the integration allow-list are also written to the outbox. Implementations
/// therefore carry the actor and timestamp needed for an audit row without any consumer
/// having to reach back into infrastructure to reconstruct them.
/// </remarks>
public interface IDomainEvent
{
    /// <summary>Type of the entity the event is about, used as the polymorphic audit subject.</summary>
    string SubjectType { get; }

    Guid SubjectId { get; }

    /// <summary>Stable, machine-readable name of what happened, such as <c>QuoteAccepted</c>.</summary>
    string Action { get; }

    /// <summary>Human-readable one-line description for the activity timeline.</summary>
    string Summary { get; }

    Guid ActorId { get; }

    DateTimeOffset OccurredAt { get; }
}
