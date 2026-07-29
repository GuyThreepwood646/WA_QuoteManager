using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuoteManager.Application.Messaging;

namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// The local consumer standing on the other end of <see cref="InProcessIntegrationEventPublisher"/>'s
/// channel — only registered when the Service Bus adapter isn't selected (AD-6).
/// </summary>
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
