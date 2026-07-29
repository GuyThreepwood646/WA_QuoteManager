using QuoteManager.Domain.Common;

namespace QuoteManager.Domain.Organizations;

/// <summary>
/// An additional site for an organization, owned by the <see cref="Organization"/> aggregate.
/// </summary>
public sealed class OrganizationLocation : Entity
{
    private OrganizationLocation(Guid id, Guid organizationId, string address, string? phone, int sortOrder)
        : base(id)
    {
        OrganizationId = organizationId;
        Address = address;
        Phone = phone;
        SortOrder = sortOrder;
    }

    // EF Core materialisation.
    private OrganizationLocation()
    {
    }

    public Guid OrganizationId { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public int SortOrder { get; private set; }

    internal void Update(string address, string? phone, int sortOrder)
    {
        Address = address;
        Phone = phone;
        SortOrder = sortOrder;
    }

    internal static OrganizationLocation Create(Guid organizationId, string address, string? phone, int sortOrder) =>
        new(Guid.CreateVersion7(), organizationId, address, phone, sortOrder);
}
