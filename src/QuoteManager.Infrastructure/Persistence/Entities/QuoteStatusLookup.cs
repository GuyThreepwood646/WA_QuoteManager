using QuoteManager.Domain.Quotes;

namespace QuoteManager.Infrastructure.Persistence.Entities;

/// <summary>
/// The set of valid quote statuses, as a referenceable table — keyed by status <em>name</em>
/// rather than a surrogate integer, since AD-3's filtered index compares against the literal
/// string and a surrogate key would let the two silently drift apart.
/// </summary>
public sealed class QuoteStatusLookup
{
    /// <summary>
    /// The status, typed as <see cref="QuoteStatus"/> rather than <see cref="string"/> — EF matches
    /// foreign keys on CLR type, not storage type, so a string key would not bind to the quote's
    /// enum column even though both persist as identical text.
    /// </summary>
    public required QuoteStatus Status { get; init; }

    public int DisplayOrder { get; init; }

    /// <summary>Whether the lifecycle ends here, so the UI can style closed quotes without
    /// re-deriving the rule.</summary>
    public bool IsTerminal { get; init; }
}
