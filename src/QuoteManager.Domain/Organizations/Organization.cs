using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;

namespace QuoteManager.Domain.Organizations;

/// <summary>
/// Which side of a request an organization sits on. Modelled explicitly, rather than inferred
/// from how a row happens to be referenced, so a request can't be raised against a vendor by accident.
/// </summary>
public enum OrganizationKind
{
    Client,
    Vendor,
}

public sealed class Organization : AggregateRoot
{
    private Organization(Guid id, string name, OrganizationKind kind, Guid actorId, DateTimeOffset createdAt)
        : base(id)
    {
        Name = name;
        Kind = kind;
        CreatedAt = createdAt;
        Raise(new OrganizationCreated(id, name, kind, actorId, createdAt));
    }

    // EF Core materialisation.
    private Organization()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public OrganizationKind Kind { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    public bool IsRetired => RetiredAt is not null;

    public static Organization Create(string name, OrganizationKind kind, DomainActor actor, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!actor.IsAdmin)
        {
            throw new OrganizationActionNotPermittedException(nameof(Create));
        }

        return new Organization(Guid.CreateVersion7(now), name.Trim(), kind, actor.Id, now);
    }

    public void Rename(string name, DomainActor actor, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!actor.IsAdmin)
        {
            throw new OrganizationActionNotPermittedException(nameof(Rename));
        }

        var trimmed = name.Trim();
        if (string.Equals(trimmed, Name, StringComparison.Ordinal))
        {
            return;
        }

        var previous = Name;
        Name = trimmed;
        Raise(new OrganizationRenamed(Id, previous, trimmed, actor.Id, now));
    }

    /// <summary>
    /// Soft-deletes the organization: it stops being offered for new associations, but existing
    /// requests and quotes that already reference it are untouched.
    /// </summary>
    public void Retire(DomainActor actor, DateTimeOffset now)
    {
        if (!actor.IsAdmin)
        {
            throw new OrganizationActionNotPermittedException(nameof(Retire));
        }

        if (IsRetired)
        {
            return;
        }

        RetiredAt = now;
        Raise(new OrganizationRetired(Id, actor.Id, now));
    }
}

public sealed record OrganizationCreated(
    Guid OrganizationId,
    string Name,
    OrganizationKind Kind,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Organization);

    public Guid SubjectId => OrganizationId;

    public string Action => nameof(OrganizationCreated);

    public string Summary => $"Created {Kind.ToString().ToLowerInvariant()} organization '{Name}'.";
}

public sealed record OrganizationRenamed(
    Guid OrganizationId,
    string PreviousName,
    string NewName,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Organization);

    public Guid SubjectId => OrganizationId;

    public string Action => nameof(OrganizationRenamed);

    public string Summary => $"Renamed organization from '{PreviousName}' to '{NewName}'.";
}

public sealed record OrganizationRetired(
    Guid OrganizationId,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string SubjectType => nameof(Organization);

    public Guid SubjectId => OrganizationId;

    public string Action => nameof(OrganizationRetired);

    public string Summary => "Retired organization.";
}
