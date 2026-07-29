using QuoteManager.Application.Messaging;

namespace QuoteManager.Application.Abstractions;

/// <summary>
/// The one port by which an integration event leaves the process. Two adapters exist (in-process
/// channel, Azure Service Bus), selected in <c>Infrastructure/DependencyInjection.cs</c>; callers
/// depend on this interface only.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
}
