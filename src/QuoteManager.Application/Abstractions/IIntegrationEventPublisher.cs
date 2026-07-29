using QuoteManager.Application.Messaging;

namespace QuoteManager.Application.Abstractions;

/// <summary>
/// The one port, per AD-6, by which an integration event actually leaves the process.
/// </summary>
/// <remarks>
/// Exactly two adapters exist: an in-process channel used by default, and an Azure Service Bus
/// adapter selected only when its connection string is configured. Selection happens in one
/// composition-root file (<c>Infrastructure/DependencyInjection.cs</c>); the outbox dispatcher and
/// any future use-case code depend on this interface only, never on a concrete adapter.
/// </remarks>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
}
