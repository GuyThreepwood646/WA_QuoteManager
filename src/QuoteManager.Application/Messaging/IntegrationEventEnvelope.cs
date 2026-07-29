namespace QuoteManager.Application.Messaging;

/// <summary>
/// An already-serialised integration event, the only shape publisher adapters ever see — it
/// carries no EF Core or Domain types, so the outbox's storage shape is free to change independently.
/// </summary>
public sealed record IntegrationEventEnvelope(
    Guid Id,
    string ContractName,
    string Payload,
    DateTimeOffset OccurredAt);
