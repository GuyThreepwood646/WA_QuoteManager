namespace QuoteManager.Domain.Quotes;

/// <summary>
/// The quote lifecycle states.
/// </summary>
/// <remarks>
/// Persisted as strings, not ordinals, so the filtered unique index in AD-3 can be expressed
/// against a readable value and inserting a state later cannot silently renumber stored rows.
/// </remarks>
public enum QuoteStatus
{
    Draft,
    Submitted,
    UnderReview,
    Accepted,
    Rejected,
    Withdrawn,
    Expired,
}

/// <summary>
/// Actions a caller may attempt against a quote.
/// </summary>
/// <remarks>
/// <see cref="Edit"/> is not a lifecycle transition but travels in the same permitted-action set,
/// per AD-7: if the UI had to derive editability from status on its own, that is precisely the
/// client-side rule duplication AD-7 forbids.
/// </remarks>
public enum QuoteAction
{
    Submit,
    StartReview,
    ReturnToSubmitted,
    Accept,
    Reject,
    Withdraw,
    Expire,
    Edit,
}
