namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// Bound from configuration, never read ad hoc via <c>IConfiguration</c> outside the composition
/// root. Absence of <see cref="ConnectionString"/> is what selects the local adapter - it's a
/// supported configuration, not a missing setting.
/// </summary>
public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string? ConnectionString { get; set; }

    /// <summary>Queue integration events are sent to. Not created automatically - provisioning the queue is out of scope here.</summary>
    public string QueueName { get; set; } = "quotemanager-integration-events";
}
