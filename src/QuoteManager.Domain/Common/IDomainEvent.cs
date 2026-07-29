namespace QuoteManager.Domain.Common;

/// <summary>
/// A fact about something that already happened inside an aggregate.
/// </summary>
/// <remarks>
/// Every domain event gets written to the audit trail; only the ones on the integration
/// allow-list also go to the outbox. So each event carries the actor and timestamp an audit row
/// needs, without infrastructure code having to reconstruct them from somewhere else.
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
