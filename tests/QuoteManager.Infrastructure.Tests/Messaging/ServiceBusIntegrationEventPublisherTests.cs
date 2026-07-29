using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using QuoteManager.Application.Messaging;
using QuoteManager.Infrastructure.Messaging;

namespace QuoteManager.Infrastructure.Tests.Messaging;

/// <summary>
/// A compile-verified Service Bus contract test. Excluded from the default run via the
/// <c>Requires=Azure</c> trait, since there's no live Azure subscription for this project to
/// target; run explicitly with <c>dotnet test --filter "Requires=Azure"</c> once a live namespace
/// and connection string exist.
/// </summary>
[Trait("Requires", "Azure")]
public sealed class ServiceBusIntegrationEventPublisherTests
{
    private const string EnvironmentVariableName = "QUOTEMANAGER_SERVICEBUS_CONNECTION_STRING";

    [Fact]
    public async Task Publishing_sends_a_message_to_the_configured_queue()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip($"Set {EnvironmentVariableName} to run this test against a live Azure Service Bus namespace.");
        }

        var options = new ServiceBusOptions { ConnectionString = connectionString };
        await using var client = new ServiceBusClient(options.ConnectionString);
        await using var sender = client.CreateSender(options.QueueName);
        var publisher = new ServiceBusIntegrationEventPublisher(sender, NullLogger<ServiceBusIntegrationEventPublisher>.Instance);

        var envelope = new IntegrationEventEnvelope(
            Guid.CreateVersion7(),
            "RequestCreated.v1",
            """{"requestId":"019a0000-0000-7000-8000-000000000000"}""",
            DateTimeOffset.UtcNow);

        await Should.NotThrowAsync(() => publisher.PublishAsync(envelope, TestContext.Current.CancellationToken));
    }
}
