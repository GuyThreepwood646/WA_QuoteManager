using System.Text.Json.Serialization;

namespace QuoteManager.Application.Messaging;

/// <summary>
/// A versioned contract that may leave the process as an integration event.
/// </summary>
/// <remarks>
/// Deliberately its own type per event, never a Domain type reused across the boundary - a
/// Domain-internal rename or field addition must not silently change what a consumer outside the
/// process already depends on.
/// </remarks>
public interface IIntegrationEvent
{
    /// <summary>Stable, versioned name a consumer dispatches on, e.g. <c>QuoteAccepted.v1</c>.</summary>
    [JsonIgnore]
    string ContractName { get; }
}

public sealed record RequestCreatedIntegrationEvent(
    Guid RequestId,
    string Title,
    Guid ClientOrganizationId,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    [JsonIgnore]
    public string ContractName => "RequestCreated.v1";
}

public sealed record VendorInvitedIntegrationEvent(
    Guid RequestId,
    Guid VendorOrganizationId,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    [JsonIgnore]
    public string ContractName => "VendorInvited.v1";
}

public sealed record QuoteAcceptedIntegrationEvent(
    Guid RequestId,
    Guid QuoteId,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    [JsonIgnore]
    public string ContractName => "QuoteAccepted.v1";
}

public sealed record RequestAwardedIntegrationEvent(
    Guid RequestId,
    Guid AcceptedQuoteId,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    [JsonIgnore]
    public string ContractName => "RequestAwarded.v1";
}

public sealed record RequestCancelledIntegrationEvent(
    Guid RequestId,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    [JsonIgnore]
    public string ContractName => "RequestCancelled.v1";
}
