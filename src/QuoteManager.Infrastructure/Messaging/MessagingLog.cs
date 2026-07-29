using Microsoft.Extensions.Logging;

namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// Source-generated log messages for outbox dispatch and integration event publication.
/// </summary>
internal static partial class MessagingLog
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Published integration event {ContractName} ({MessageId}) via the in-process channel adapter")]
    public static partial void PublishedLocally(ILogger logger, Guid messageId, string contractName);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Published integration event {ContractName} ({MessageId}) to Azure Service Bus")]
    public static partial void PublishedToServiceBus(ILogger logger, Guid messageId, string contractName);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Error,
        Message = "Failed to publish outbox message {MessageId} ({ContractName}), attempt {Attempt}")]
    public static partial void PublishFailed(ILogger logger, Exception exception, Guid messageId, string contractName, int attempt);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Error,
        Message = "Outbox dispatch loop failed unexpectedly; will retry after the next poll interval")]
    public static partial void DispatchLoopFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1106,
        Level = LogLevel.Information,
        Message = "Local queue delivered {ContractName} ({MessageId}), occurred at {OccurredAt}")]
    public static partial void LocalQueueDelivered(ILogger logger, Guid messageId, string contractName, DateTimeOffset occurredAt);
}
