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
    private readonly List<OrganizationLocation> _locations = [];

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

    public string? PrimaryAddress { get; private set; }

    public string? PrimaryContactName { get; private set; }

    public string? PrimaryContactEmail { get; private set; }

    public string? PrimaryContactPhone { get; private set; }

    public bool IsPreferredVendor { get; private set; }

    public IReadOnlyList<OrganizationLocation> Locations => _locations;

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

    public void UpdateProfile(
        string name,
        string? primaryAddress,
        string? primaryContactName,
        string? primaryContactEmail,
        string? primaryContactPhone,
        bool isPreferredVendor,
        IEnumerable<OrganizationLocationInput> locations,
        DomainActor actor,
        DateTimeOffset now)
    {
        if (!actor.IsAdmin)
        {
            throw new OrganizationActionNotPermittedException(nameof(UpdateProfile));
        }

        var versionBefore = Version;
        Rename(name, actor, now);

        PrimaryAddress = NormalizeOptional(primaryAddress, 500);
        PrimaryContactName = NormalizeOptional(primaryContactName, 200);
        PrimaryContactEmail = NormalizeOptional(primaryContactEmail, 320);
        PrimaryContactPhone = NormalizeOptional(primaryContactPhone, 50);
        IsPreferredVendor = Kind == OrganizationKind.Vendor && isPreferredVendor;

        ReplaceLocations(locations);

        if (Version == versionBefore)
        {
            MarkModified();
        }
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

    private void ReplaceLocations(IEnumerable<OrganizationLocationInput> locations)
    {
        var normalized = locations
            .Select(location => new
            {
                Address = NormalizeOptional(location.Address, 500),
                Phone = NormalizeOptional(location.Phone, 50),
            })
            .Where(location => location.Address is not null)
            .ToList();

        var sortOrder = 0;
        foreach (var location in normalized)
        {
            if (sortOrder < _locations.Count)
            {
                _locations[sortOrder].Update(location.Address!, location.Phone, sortOrder);
            }
            else
            {
                _locations.Add(OrganizationLocation.Create(Id, location.Address!, location.Phone, sortOrder));
            }

            sortOrder++;
        }

        while (_locations.Count > sortOrder)
        {
            _locations.RemoveAt(_locations.Count - 1);
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
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
