using QuoteManager.Domain.Common;

namespace QuoteManager.Domain.Organizations;

/// <summary>
/// Which side of a request an organisation sits on.
/// </summary>
/// <remarks>
/// Modelled explicitly rather than inferred from whether rows happen to reference the
/// organisation as a client or a vendor, so the two roles one entity plays are visible in the
/// schema and a request cannot be raised on behalf of a supplier by accident.
/// </remarks>
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

    public static Organization Create(string name, OrganizationKind kind, Guid actorId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Organization(Guid.CreateVersion7(now), name.Trim(), kind, actorId, now);
    }

    public void Rename(string name, Guid actorId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmed = name.Trim();
        if (string.Equals(trimmed, Name, StringComparison.Ordinal))
        {
            return;
        }

        var previous = Name;
        Name = trimmed;
        Raise(new OrganizationRenamed(Id, previous, trimmed, actorId, now));
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

    public string Summary => $"Created {Kind.ToString().ToLowerInvariant()} organisation '{Name}'.";
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

    public string Summary => $"Renamed organisation from '{PreviousName}' to '{NewName}'.";
}
