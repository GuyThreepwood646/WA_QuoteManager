namespace QuoteManager.Domain.Common;

/// <summary>
/// Base class for entities that are the transactional boundary of a change.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id) => Id = id;

    // EF Core materialisation.
    protected AggregateRoot()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Incremented on every state change and mapped as an EF concurrency token.
    /// </summary>
    /// <remarks>
    /// SQLite has no <c>rowversion</c> type, so EF can't generate this for us — hence a plain
    /// integer instead of <c>IsRowVersion()</c>.
    /// </remarks>
    public int Version { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
        Version++;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
