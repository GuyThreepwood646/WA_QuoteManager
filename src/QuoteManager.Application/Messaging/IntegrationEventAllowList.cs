using QuoteManager.Domain.Common;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;

namespace QuoteManager.Application.Messaging;

/// <summary>
/// The single place deciding which domain events also leave the process as integration events. A
/// domain event absent here is still audited, just never queued. <see cref="QuoteStatusChanged"/>
/// carries every quote transition, so it's pattern-matched on <c>To == QuoteStatus.Accepted</c>
/// rather than allow-listed wholesale.
/// </summary>
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
