namespace QuoteManager.Domain.Quotes;

/// <summary>
/// The quote lifecycle states.
/// </summary>
/// <remarks>
/// Persisted as strings, not ordinals, so the filtered unique index enforcing "at most one
/// accepted quote per request" can be expressed against a readable value, and inserting a new
/// state later can't silently renumber stored rows.
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
/// <see cref="Edit"/> is not a lifecycle transition but travels in the same permitted-action set
/// so the UI never has to derive editability from status on its own.
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
