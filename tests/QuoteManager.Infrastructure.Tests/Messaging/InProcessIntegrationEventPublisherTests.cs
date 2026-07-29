using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using QuoteManager.Application.Messaging;
using QuoteManager.Infrastructure.Messaging;

namespace QuoteManager.Infrastructure.Tests.Messaging;

/// <summary>
/// AD-6's contract test suite against the local adapter, which runs unconditionally in CI (unlike
/// the Service Bus adapter's own contract test, which is gated behind a live connection string).
/// </summary>
public sealed class InProcessIntegrationEventPublisherTests
{
    [Fact]
    public async Task Publishing_writes_the_envelope_to_the_channel_for_the_local_consumer_to_read()
    {
        var channel = Channel.CreateUnbounded<IntegrationEventEnvelope>();
        var publisher = new InProcessIntegrationEventPublisher(channel, NullLogger<InProcessIntegrationEventPublisher>.Instance);
        var envelope = new IntegrationEventEnvelope(
            Guid.CreateVersion7(),
            "RequestCreated.v1",
            """{"requestId":"019a0000-0000-7000-8000-000000000000"}""",
            DateTimeOffset.UtcNow);

        await publisher.PublishAsync(envelope, TestContext.Current.CancellationToken);

        channel.Reader.TryRead(out var delivered).ShouldBeTrue();
        delivered.ShouldBe(envelope);
    }
}
