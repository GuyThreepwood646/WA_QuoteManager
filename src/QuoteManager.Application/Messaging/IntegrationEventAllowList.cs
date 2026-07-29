using QuoteManager.Domain.Common;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;

namespace QuoteManager.Application.Messaging;

/// <summary>
/// The single place that decides which domain events also leave the process as integration
/// events, and the versioned, Domain-type-free shape each takes on the wire.
/// </summary>
/// <remarks>
/// A domain event absent from <see cref="Resolve"/> is still written to the audit trail and
/// nowhere else - the outbox carries out-of-boundary effects that another system needs to react to,
/// not an undifferentiated firehose of every internal state change. <see cref="QuoteStatusChanged"/>
/// carries every quote transition, not just acceptance, so it is pattern-matched on
/// <c>To == QuoteStatus.Accepted</c> rather than allow-listed wholesale.
/// </remarks>
public static class IntegrationEventAllowList
{
    public static IIntegrationEvent? Resolve(IDomainEvent domainEvent) => domainEvent switch
    {
        RequestCreated e => new RequestCreatedIntegrationEvent(e.RequestId, e.Title, e.ClientOrganizationId, e.OccurredAt),
        VendorInvited e => new VendorInvitedIntegrationEvent(e.RequestId, e.VendorOrganizationId, e.OccurredAt),
        RequestAwarded e => new RequestAwardedIntegrationEvent(e.RequestId, e.AcceptedQuoteId, e.OccurredAt),
        RequestCancelled e => new RequestCancelledIntegrationEvent(e.RequestId, e.OccurredAt),
        QuoteStatusChanged { To: QuoteStatus.Accepted } e => new QuoteAcceptedIntegrationEvent(e.RequestId, e.QuoteId, e.OccurredAt),
        _ => null,
    };
}
