namespace QuoteManager.Domain.Identity;

/// <summary>
/// Who is performing an operation.
/// </summary>
/// <remarks>
/// Only ever constructed from the authenticated principal, or from <see cref="System"/> for work
/// with no HTTP caller — never from a request payload, since an actor the caller gets to choose
/// makes the audit trail worthless.
///
/// <see cref="OrganizationId"/> is null for platform staff (Admin, Reviewer) who don't act for a
/// single organization. It's what lets the transition table enforce that a Vendor may only touch
/// its own organization's quotes, instead of trusting the role claim alone.
/// </remarks>
public sealed record DomainActor(Guid Id, string DisplayName, AppRole Roles, Guid? OrganizationId)
{
    /// <summary>
    /// The actor for background work, so an audit row always has a resolvable origin.
    /// </summary>
    public static DomainActor System { get; } =
        new(Guid.Empty, "System", AppRole.Admin, OrganizationId: null);

    public bool IsAdmin => Roles.HasAny(AppRole.Admin);

    /// <summary>
    /// Whether this actor may act on behalf of a vendor organization.
    /// </summary>
    /// <remarks>
    /// The whole ownership rule lives here so both places a vendor gate applies — drafting a quote
    /// and transitioning one — call this instead of repeating the comparison. Two copies of an
    /// authorisation check is how one of them gets fixed later and the other doesn't.
    ///
    /// An actor with no organization fails deliberately: platform staff (Reviewer, Requester) act
    /// for no vendor, so having no organization claim grants no vendor capability. Admin is the one
    /// explicit bypass.
    /// </remarks>
    public bool CanActForVendorOrganization(Guid vendorOrganizationId) =>
        IsAdmin || (OrganizationId is { } organizationId && organizationId == vendorOrganizationId);
}
