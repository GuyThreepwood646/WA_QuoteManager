namespace QuoteManager.Domain.Common;

/// <summary>
/// A fact about something that already happened inside an aggregate. Every event carries the
/// actor and timestamp an audit row needs directly, since every event is audited but only some
/// also reach the outbox.
/// </summary>
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

    /// <summary>Optional free-text note the actor typed when they made this happen. Most events don't carry one.</summary>
    string? Note => null;
}
