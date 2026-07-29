using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using QuoteManager.Application.Abstractions;
using QuoteManager.Application.Messaging;

namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// The default adapter for <see cref="IIntegrationEventPublisher"/> — an in-process channel,
/// selected whenever <see cref="ServiceBusOptions.ConnectionString"/> is absent (AD-6). Writing to
/// the channel is itself the "publish"; <see cref="InProcessIntegrationEventLogger"/> is the one
/// local consumer.
/// </summary>
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
