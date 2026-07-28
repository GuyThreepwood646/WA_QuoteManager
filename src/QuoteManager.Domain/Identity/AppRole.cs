namespace QuoteManager.Domain.Identity;

/// <summary>
/// The closed set of roles, per AD-13.
/// </summary>
/// <remarks>
/// Flags rather than a plain enum because a single transition is usually permitted to more than
/// one role, so the transition table can carry the permitted set in one field. A user's roles
/// combine the same way, which reduces the authorisation check to one bitwise test.
/// </remarks>
[Flags]
public enum AppRole
{
    None = 0,

    /// <summary>Raises and manages requests on behalf of a client organisation.</summary>
    Requester = 1 << 0,

    /// <summary>Moves quotes through review and decides the outcome.</summary>
    Reviewer = 1 << 1,

    /// <summary>Creates, submits, and withdraws quotes for its own organisation.</summary>
    Vendor = 1 << 2,

    Admin = 1 << 3,

    All = Requester | Reviewer | Vendor | Admin,
}

public static class AppRoleExtensions
{
    /// <summary>
    /// Whether <paramref name="actorRoles"/> includes any of <paramref name="permitted"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="AppRole.Admin"/> is deliberately not special-cased here. It is granted every
    /// permission by being listed in the transition table, so that the table remains a complete
    /// and readable statement of who may do what, with no implicit override hidden in code.
    /// </remarks>
    public static bool HasAny(this AppRole actorRoles, AppRole permitted) => (actorRoles & permitted) != AppRole.None;
}
