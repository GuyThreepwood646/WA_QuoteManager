using QuoteManager.Domain.Identity;

namespace QuoteManager.Domain.Quotes;

/// <summary>
/// One legal move: from a state, by an action, to a state, permitted to a set of roles.
/// </summary>
public sealed record QuoteTransition(
    QuoteStatus From,
    QuoteAction Action,
    QuoteStatus To,
    AppRole PermittedRoles);

/// <summary>
/// The single authority on the quote lifecycle, per AD-2.
/// </summary>
/// <remarks>
/// Both the API's authorisation check and the permitted-action set projected to the client call
/// <see cref="PermittedFor"/>. Nothing else may decide whether an action is legal: a second
/// opinion expressed as an <c>[Authorize(Roles = "...")]</c> attribute on a transition endpoint
/// would let the UI offer actions the API refuses, which is the exact drift AD-7 exists to stop.
/// </remarks>
public static class QuoteTransitions
{
    private const AppRole VendorSide = AppRole.Vendor | AppRole.Admin;
    private const AppRole ReviewerSide = AppRole.Reviewer | AppRole.Admin;

    private static readonly QuoteTransition[] Table =
    [
        new(QuoteStatus.Draft, QuoteAction.Submit, QuoteStatus.Submitted, VendorSide),
        new(QuoteStatus.Draft, QuoteAction.Edit, QuoteStatus.Draft, VendorSide),
        new(QuoteStatus.Draft, QuoteAction.Withdraw, QuoteStatus.Withdrawn, VendorSide),

        new(QuoteStatus.Submitted, QuoteAction.StartReview, QuoteStatus.UnderReview, ReviewerSide),
        new(QuoteStatus.Submitted, QuoteAction.Withdraw, QuoteStatus.Withdrawn, VendorSide),
        new(QuoteStatus.Submitted, QuoteAction.Expire, QuoteStatus.Expired, AppRole.Admin),

        new(QuoteStatus.UnderReview, QuoteAction.Accept, QuoteStatus.Accepted, ReviewerSide),
        new(QuoteStatus.UnderReview, QuoteAction.Reject, QuoteStatus.Rejected, ReviewerSide),
        new(QuoteStatus.UnderReview, QuoteAction.ReturnToSubmitted, QuoteStatus.Submitted, ReviewerSide),
        new(QuoteStatus.UnderReview, QuoteAction.Expire, QuoteStatus.Expired, AppRole.Admin),
    ];

    public static IReadOnlyList<QuoteTransition> All => Table;

    /// <summary>
    /// The terminal states, from which no action is legal for anyone.
    /// </summary>
    public static bool IsTerminal(QuoteStatus status) =>
        status is QuoteStatus.Accepted or QuoteStatus.Rejected or QuoteStatus.Withdrawn or QuoteStatus.Expired;

    /// <summary>
    /// Every action <paramref name="actorRoles"/> may take on a quote in <paramref name="status"/>.
    /// </summary>
    public static IReadOnlyList<QuoteAction> PermittedFor(QuoteStatus status, AppRole actorRoles)
    {
        var permitted = new List<QuoteAction>();

        foreach (var transition in Table)
        {
            if (transition.From == status && actorRoles.HasAny(transition.PermittedRoles))
            {
                permitted.Add(transition.Action);
            }
        }

        return permitted;
    }

    /// <summary>
    /// Resolves an attempted action, distinguishing "illegal from this state" from
    /// "legal but not for these roles".
    /// </summary>
    /// <remarks>
    /// The distinction is not cosmetic: the first is a 409 telling the user the world moved on,
    /// the second a 403 telling them to find a colleague. Collapsing them would make both
    /// misleading.
    /// </remarks>
    public static TransitionResolution Resolve(QuoteStatus status, QuoteAction action, AppRole actorRoles)
    {
        foreach (var transition in Table)
        {
            if (transition.From != status || transition.Action != action)
            {
                continue;
            }

            return actorRoles.HasAny(transition.PermittedRoles)
                ? TransitionResolution.Allowed(transition.To)
                : TransitionResolution.DeniedByRole();
        }

        return TransitionResolution.NotLegalFromState();
    }

    /// <summary>
    /// Whether a quote's business fields may still be changed, per AD-2's mutability rule.
    /// </summary>
    public static bool IsEditable(QuoteStatus status) => status is QuoteStatus.Draft;
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
