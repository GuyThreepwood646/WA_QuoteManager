namespace QuoteManager.Domain.Identity;

/// <summary>
/// Who is performing an operation.
/// </summary>
/// <remarks>
/// AD-10: this is only ever constructed from the authenticated principal, or from
/// <see cref="System"/> for work with no HTTP caller. No request payload may supply one, since an
/// actor a caller can choose makes the whole audit trail inadmissible.
/// </remarks>
public sealed record DomainActor(Guid Id, string DisplayName, AppRole Roles)
{
    /// <summary>
    /// The actor for background work, so an audit row always has a resolvable origin.
    /// </summary>
    public static DomainActor System { get; } =
        new(Guid.Empty, "System", AppRole.Admin);
}
