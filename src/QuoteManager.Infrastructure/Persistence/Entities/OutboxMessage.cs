namespace QuoteManager.Infrastructure.Persistence.Entities;

/// <summary>
/// An integration event awaiting publication — written in the same transaction as the state
/// change it represents, and only for events on the AD-4 integration allow-list.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>The integration event contract name, which is what a consumer dispatches on.</summary>
    public required string Type { get; init; }

    public required string Payload { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? DispatchedAt { get; set; }

    /// <summary>
    /// Number of delivery attempts, so a permanently failing message is visible rather than
    /// retried silently forever.
    /// </summary>
    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
