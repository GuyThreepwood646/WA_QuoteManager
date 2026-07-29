namespace QuoteManager.Domain.Identity;

/// <summary>
/// Who is performing an operation. Only ever built from the authenticated principal (or
/// <see cref="System"/> for callerless work), never from a request payload.
/// </summary>
/// <remarks>
/// <see cref="OrganizationId"/> is not Vendor-specific — a Requester or Reviewer carries their
/// client organization here too — so it must always be paired with a role check, not used alone.
/// </remarks>
public sealed record DomainActor(Guid Id, string DisplayName, AppRole Roles, Guid? OrganizationId)
{
    /// <summary>The actor for background work, so an audit row always has a resolvable origin.</summary>
    public static DomainActor System { get; } =
        new(Guid.Empty, "System", AppRole.Admin, OrganizationId: null);

    public bool IsAdmin => Roles.HasAny(AppRole.Admin);

    /// <summary>
    /// Whether this actor's organization id matches <paramref name="vendorOrganizationId"/> (Admin
    /// always matches). Compares ids only — callers must separately check for the Vendor role.
    /// </summary>
    public bool CanActForVendorOrganization(Guid vendorOrganizationId) =>
        IsAdmin || (OrganizationId is { } organizationId && organizationId == vendorOrganizationId);
}
