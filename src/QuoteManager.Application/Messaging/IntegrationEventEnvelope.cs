namespace QuoteManager.Application.Messaging;

/// <summary>
/// An already-serialised integration event ready to leave the process.
/// </summary>
/// <remarks>
/// This is the only shape <see cref="QuoteManager.Application.Abstractions.IIntegrationEventPublisher"/>
/// adapters ever see. It carries no EF Core or Domain types, so a message broker adapter never
/// needs to know how the outbox itself is persisted, and the outbox's own storage shape is free to
/// change without touching either adapter.
/// </remarks>
public sealed record IntegrationEventEnvelope(
    Guid Id,
    string ContractName,
    string Payload,
    DateTimeOffset OccurredAt);
