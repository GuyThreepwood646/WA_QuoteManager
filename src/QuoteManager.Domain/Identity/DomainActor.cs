namespace QuoteManager.Domain.Identity;

/// <summary>
/// Who is performing an operation.
/// </summary>
/// <remarks>
/// AD-10: this is only ever constructed from the authenticated principal, or from
/// <see cref="System"/> for work with no HTTP caller. No request payload may supply one, since an
/// actor a caller can choose makes the whole audit trail inadmissible.
///
/// <see cref="OrganizationId"/> is null for platform staff (Admin, Reviewer) who act for no single
/// organisation, and is what lets the transition table enforce that a Vendor may only act on its
/// own organisation's quotes rather than trusting the role claim alone.
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
    /// Whether this actor may act on behalf of a vendor organisation.
    /// </summary>
    /// <remarks>
    /// The whole of AD-13's ownership rule, in one place. Both gates a vendor can reach — creating a
    /// quote and transitioning one — call this rather than restating the comparison, because two
    /// copies of an authorisation rule is how one of them ends up fixed and the other forgotten.
    ///
    /// An actor with no organisation fails deliberately: platform staff (Reviewer, Requester) act
    /// for no vendor, so they get no vendor-gated capability from the absence of a claim. Admin is
    /// the single explicit bypass.
    /// </remarks>
    public bool CanActForVendorOrganization(Guid vendorOrganizationId) =>
        IsAdmin || (OrganizationId is { } organizationId && organizationId == vendorOrganizationId);
}
