namespace QuoteManager.Domain.Common;

/// <summary>
/// An entity that lives inside an aggregate and so cannot raise events or be saved on its own.
/// Carries its own <see cref="Version"/> so concurrency is checked per-entity, not just per
/// aggregate — two reviewers acting on different quotes of the same request shouldn't collide.
/// </summary>
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
