using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QuoteManager.Application.Abstractions;
using QuoteManager.Application.Messaging;

namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// AD-6's Azure adapter for <see cref="IIntegrationEventPublisher"/>: selected only when
/// <see cref="ServiceBusOptions.ConnectionString"/> is configured.
/// </summary>
/// <remarks>
/// <see cref="ServiceBusMessage.MessageId"/> is set to the outbox message's own id, so a consumer
/// enabling Service Bus's duplicate-detection window gets exactly-once delivery for free within
/// that window - required, not optional, given AD-4's at-least-once guarantee on the sending side.
/// <see cref="ServiceBusMessage.Subject"/> carries the versioned contract name, so a consumer can
/// dispatch without deserialising the body first.
/// </remarks>
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
