using QuoteManager.Domain.Identity;

namespace QuoteManager.Domain.Quotes;

/// <summary>
/// One legal move: from a state, by an action, to a state, permitted to a set of roles.
/// </summary>
/// <remarks>
/// <paramref name="IsVendorGated"/> marks rows whose <see cref="PermittedRoles"/> is the Vendor
/// side of the table. A Vendor may only act on its own organization's quotes, and a role check
/// alone can't express "this vendor, not any vendor" — so these rows carry an extra
/// organization-matching requirement that Reviewer/Admin-only rows don't need.
/// </remarks>
public sealed record QuoteTransition(
    QuoteStatus From,
    QuoteAction Action,
    QuoteStatus To,
    AppRole PermittedRoles,
    bool IsVendorGated);

/// <summary>
/// The single authority on the quote lifecycle.
/// </summary>
/// <remarks>
/// Both the API's authorisation check and the permitted-action set projected to the client call
/// <see cref="PermittedFor"/>. Nothing else may decide whether an action is legal: a second
/// opinion expressed as an <c>[Authorize(Roles = "...")]</c> attribute on a transition endpoint
/// would let the UI offer actions the API refuses, and the two would quietly drift apart over time.
/// </remarks>
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
    /// Resolves an attempted action, distinguishing "illegal from this state" from
    /// "legal but not for this actor".
    /// </summary>
    /// <remarks>
    /// The distinction is not cosmetic: the first is a 409 telling the user the world moved on,
    /// the second a 403 telling them to find a colleague. Collapsing them would make both
    /// misleading.
    /// </remarks>
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

        // Role alone cannot tell one vendor from another. Ownership lives on DomainActor so both
        // gates that need it - this table and Request.AddQuote - ask the same question.
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
