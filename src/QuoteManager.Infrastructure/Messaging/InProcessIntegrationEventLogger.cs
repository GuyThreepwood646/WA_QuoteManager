using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuoteManager.Application.Messaging;

namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// The local consumer standing on the other end of <see cref="InProcessIntegrationEventPublisher"/>'s
/// channel, so the demo's default adapter is a working local queue with a reader, not a
/// fire-and-forget call into nothing.
/// </summary>
/// <remarks>
/// Only registered when the Service Bus adapter is not selected (see
/// <c>Infrastructure/DependencyInjection.cs</c>) - a real broker needs no in-process stand-in
/// consumer, since whatever holds the Service Bus connection string is the real consumer.
/// </remarks>
public sealed class InProcessIntegrationEventLogger(
    Channel<IntegrationEventEnvelope> channel,
    ILogger<InProcessIntegrationEventLogger> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var envelope in channel.Reader.ReadAllAsync(stoppingToken))
        {
            MessagingLog.LocalQueueDelivered(logger, envelope.Id, envelope.ContractName, envelope.OccurredAt);
        }
    }
}
