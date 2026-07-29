namespace QuoteManager.Domain.Common;

/// <summary>
/// An entity that lives inside an aggregate and so cannot raise events or be saved on its own.
/// </summary>
/// <remarks>
/// Carries its own <see cref="Version"/> because we concurrency-check the specific entity a
/// caller addressed, not just the aggregate — two reviewers acting on different quotes of the
/// same request shouldn't collide with each other.
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
