using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using QuoteManager.Application.Abstractions;
using QuoteManager.Application.Messaging;

namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// The default adapter for <see cref="IIntegrationEventPublisher"/>: an in-process channel, so the
/// demo has a genuine local queue to show rather than a publish call that quietly does nothing.
/// </summary>
/// <remarks>
/// Selected whenever <see cref="ServiceBusOptions.ConnectionString"/> is absent. Handing an
/// envelope to <see cref="Channel{T}.Writer"/> is itself the "publish" - the paired
/// <see cref="InProcessIntegrationEventLogger"/> hosted service is the one and only local
/// consumer, standing in for whatever process would otherwise be on the other end of a real queue.
/// </remarks>
public sealed class InProcessIntegrationEventPublisher(
    Channel<IntegrationEventEnvelope> channel,
    ILogger<InProcessIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public async Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        await channel.Writer.WriteAsync(envelope, cancellationToken);
        MessagingLog.PublishedLocally(logger, envelope.Id, envelope.ContractName);
    }
}
