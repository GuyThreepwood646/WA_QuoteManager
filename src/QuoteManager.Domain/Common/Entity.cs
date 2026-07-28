namespace QuoteManager.Domain.Common;

/// <summary>
/// An entity that lives inside an aggregate and so cannot raise events or be saved on its own.
/// </summary>
/// <remarks>
/// It still carries its own <see cref="Version"/> because AD-15 concurrency-checks the entity a
/// caller actually addressed. Two reviewers acting on different quotes of one request must not
/// collide, so the token has to be finer-grained than the aggregate.
/// </remarks>
public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    // EF Core materialisation.
    protected Entity()
    {
    }

    public Guid Id { get; private set; }

    public int Version { get; private set; }

    protected void Touch() => Version++;
}
