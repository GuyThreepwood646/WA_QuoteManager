namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// AD-6: bound from configuration, never read ad hoc via <c>IConfiguration</c> outside the
/// composition root. Absence of <see cref="ConnectionString"/> is what selects the local adapter -
/// it is a supported configuration, not a missing setting.
/// </summary>
public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string? ConnectionString { get; set; }

    /// <summary>Queue integration events are sent to. Not created automatically - provisioning is a Deferred concern.</summary>
    public string QueueName { get; set; } = "quotemanager-integration-events";
}
