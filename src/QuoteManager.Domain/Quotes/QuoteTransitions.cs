using QuoteManager.Domain.Identity;

namespace QuoteManager.Domain.Quotes;

/// <summary>
/// One legal move: from a state, by an action, to a state, permitted to a set of roles.
/// <paramref name="IsVendorGated"/> marks Vendor-side rows that also require organization
/// ownership, since a role check alone can't express "this vendor, not any vendor".
/// </summary>
public sealed record QuoteTransition(
    QuoteStatus From,
    QuoteAction Action,
    QuoteStatus To,
    AppRole PermittedRoles,
    bool IsVendorGated);

/// <summary>
/// The single authority on the quote lifecycle — nothing else (e.g. a route-level role attribute)
/// may independently decide whether an action is legal, or the two could drift apart.
/// </summary>
public static class QuoteTransitions
{
    private const AppRole VendorSide = AppRole.Vendor | AppRole.Admin;
    private const AppRole ReviewerSide = AppRole.Reviewer | AppRole.Admin;

    private static readonly QuoteTransition[] Table =
    [
        new(QuoteStatus.Draft, QuoteAction.Submit, QuoteStatus.Submitted, VendorSide, IsVendorGated: true),
        new(QuoteStatus.Draft, QuoteAction.Edit, QuoteStatus.Draft, VendorSide, IsVendorGated: true),
        new(QuoteStatus.Draft, QuoteAction.Withdraw, QuoteStatus.Withdrawn, VendorSide, IsVendorGated: true),

        new(QuoteStatus.Submitted, QuoteAction.StartReview, QuoteStatus.UnderReview, ReviewerSide, IsVendorGated: false),
        new(QuoteStatus.Submitted, QuoteAction.Withdraw, QuoteStatus.Withdrawn, VendorSide, IsVendorGated: true),
        new(QuoteStatus.Submitted, QuoteAction.Expire, QuoteStatus.Expired, AppRole.Admin, IsVendorGated: false),

        new(QuoteStatus.UnderReview, QuoteAction.Accept, QuoteStatus.Accepted, ReviewerSide, IsVendorGated: false),
        new(QuoteStatus.UnderReview, QuoteAction.Reject, QuoteStatus.Rejected, ReviewerSide, IsVendorGated: false),
        new(QuoteStatus.UnderReview, QuoteAction.ReturnToSubmitted, QuoteStatus.Submitted, ReviewerSide, IsVendorGated: false),
        new(QuoteStatus.UnderReview, QuoteAction.Expire, QuoteStatus.Expired, AppRole.Admin, IsVendorGated: false),
    ];

    public static IReadOnlyList<QuoteTransition> All => Table;

    /// <summary>
    /// The terminal states, from which no action is legal for anyone.
    /// </summary>
    public static bool IsTerminal(QuoteStatus status) =>
        status is QuoteStatus.Accepted or QuoteStatus.Rejected or QuoteStatus.Withdrawn or QuoteStatus.Expired;

    /// <summary>
    /// Every action <paramref name="actor"/> may take on a quote in <paramref name="status"/>
    /// belonging to <paramref name="vendorOrganizationId"/>.
    /// </summary>
    public static IReadOnlyList<QuoteAction> PermittedFor(QuoteStatus status, DomainActor actor, Guid vendorOrganizationId)
    {
        var permitted = new List<QuoteAction>();

        foreach (var transition in Table)
        {
            if (transition.From == status && IsPermitted(transition, actor, vendorOrganizationId))
            {
                permitted.Add(transition.Action);
            }
        }

        return permitted;
    }

    /// <summary>
    /// Resolves an attempted action, distinguishing "illegal from this state" (409: the world
    /// moved on) from "legal but not for this actor" (403: find a colleague).
    /// </summary>
    public static TransitionResolution Resolve(QuoteStatus status, QuoteAction action, DomainActor actor, Guid vendorOrganizationId)
    {
        foreach (var transition in Table)
        {
            if (transition.From != status || transition.Action != action)
            {
                continue;
            }

            return IsPermitted(transition, actor, vendorOrganizationId)
                ? TransitionResolution.Allowed(transition.To)
                : TransitionResolution.DeniedByRole();
        }

        return TransitionResolution.NotLegalFromState();
    }

    /// <summary>
    /// Whether a quote's business fields may still be changed.
    /// </summary>
    public static bool IsEditable(QuoteStatus status) => status is QuoteStatus.Draft;

    private static bool IsPermitted(QuoteTransition transition, DomainActor actor, Guid vendorOrganizationId)
    {
        if (!actor.Roles.HasAny(transition.PermittedRoles))
        {
            return false;
        }

        // Role alone can't tell one vendor from another; ownership is DomainActor's job.
        return !transition.IsVendorGated || actor.CanActForVendorOrganization(vendorOrganizationId);
    }
}

public readonly record struct TransitionResolution
{
    private TransitionResolution(bool isAllowed, bool isDeniedByRole, QuoteStatus resulting)
    {
        IsAllowed = isAllowed;
        IsDeniedByRole = isDeniedByRole;
        Resulting = resulting;
    }

    public bool IsAllowed { get; }

    public bool IsDeniedByRole { get; }

    public QuoteStatus Resulting { get; }

    internal static TransitionResolution Allowed(QuoteStatus resulting) => new(true, false, resulting);

    internal static TransitionResolution DeniedByRole() => new(false, true, default);

    internal static TransitionResolution NotLegalFromState() => new(false, false, default);
}
