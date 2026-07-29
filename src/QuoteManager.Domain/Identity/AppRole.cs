namespace QuoteManager.Domain.Identity;

/// <summary>
/// The closed set of roles a user can hold. Flags rather than a plain enum so a transition's
/// permitted role set, and a user's own roles, can each combine into one bitwise-testable value.
/// </summary>
[Flags]
public enum AppRole
{
    None = 0,

    /// <summary>Raises and manages requests on behalf of a client organization.</summary>
    Requester = 1 << 0,

    /// <summary>Moves quotes through review and decides the outcome.</summary>
    Reviewer = 1 << 1,

    /// <summary>Creates, submits, and withdraws quotes for its own organization.</summary>
    Vendor = 1 << 2,

    Admin = 1 << 3,

    All = Requester | Reviewer | Vendor | Admin,
}

public static class AppRoleExtensions
{
    /// <summary>
    /// Whether <paramref name="actorRoles"/> includes any of <paramref name="permitted"/>.
    /// <see cref="AppRole.Admin"/> is deliberately not special-cased — it's granted every
    /// permission by being listed in the transition table like any other role.
    /// </summary>
    public static bool HasAny(this AppRole actorRoles, AppRole permitted) => (actorRoles & permitted) != AppRole.None;

    /// <summary>
    /// Splits a (possibly combined) flags value into its individual roles, since roles cross the
    /// wire as one JWT claim per role / a JSON string array, neither of which is a flags value.
    /// </summary>
    public static IEnumerable<AppRole> Split(this AppRole roles) => AllValues.Where(value => roles.HasAny(value));

    private static readonly AppRole[] AllValues = [AppRole.Admin, AppRole.Requester, AppRole.Reviewer, AppRole.Vendor];
}
