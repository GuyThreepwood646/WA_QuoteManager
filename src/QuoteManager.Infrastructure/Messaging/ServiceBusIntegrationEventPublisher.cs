using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QuoteManager.Application.Abstractions;
using QuoteManager.Application.Messaging;

namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// The Azure adapter for <see cref="IIntegrationEventPublisher"/>, selected only when
/// <see cref="ServiceBusOptions.ConnectionString"/> is configured. <see cref="ServiceBusMessage.MessageId"/>
/// is set to the outbox row's own id, which is what Service Bus duplicate-detection keys on (AD-6).
/// </summary>
public sealed class ServiceBusIntegrationEventPublisher(
    ServiceBusSender sender,
    ILogger<ServiceBusIntegrationEventPublisher> logger) : IIntegrationEventPublisher
{
    public async Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var message = new ServiceBusMessage(envelope.Payload)
        {
            MessageId = envelope.Id.ToString(),
            Subject = envelope.ContractName,
            ContentType = "application/json",
        };

        await sender.SendMessageAsync(message, cancellationToken);
        MessagingLog.PublishedToServiceBus(logger, envelope.Id, envelope.ContractName);
    }
}
