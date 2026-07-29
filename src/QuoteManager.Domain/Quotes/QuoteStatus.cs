namespace QuoteManager.Domain.Quotes;

/// <summary>
/// The quote lifecycle states. Persisted as strings, not ordinals, so a new state can be added
/// later without silently renumbering stored rows.
/// </summary>
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
/// Actions a caller may attempt against a quote. <see cref="Edit"/> is not a lifecycle transition
/// but travels in the same permitted-action set so the UI never derives editability separately.
/// </summary>
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
