using QuoteManager.Domain.Quotes;

namespace QuoteManager.Infrastructure.Persistence.Entities;

/// <summary>
/// The set of valid quote statuses, as a referenceable table.
/// </summary>
/// <remarks>
/// Keyed by the status <em>name</em> rather than a surrogate integer, which is the point. An
/// integer key would force the quote's status column to become an ordinal, and the filtered unique
/// index behind the single-accepted-quote invariant compares against the literal
/// <c>'Accepted'</c> — so the index and the enum would have to be kept aligned by hand, which is
/// precisely the silent drift that index already had to be protected from.
///
/// Keying by name gives referential integrity, keeps the index working untouched, and leaves the
/// database readable. The lifecycle itself still lives in the domain transition table; this is a
/// constraint on what may be stored, not a second opinion on what may happen.
/// </remarks>
public sealed class QuoteStatusLookup
{
    /// <summary>
    /// The status, typed as the domain enum and stored as its name.
    /// </summary>
    /// <remarks>
    /// Typed as <see cref="QuoteStatus"/> rather than <see cref="string"/> because EF matches
    /// foreign keys on CLR type, not storage type: a string key here would not bind to the quote's
    /// enum column even though both persist as identical text.
    /// </remarks>
    public required QuoteStatus Status { get; init; }

    /// <summary>Presentation order for status filters and grouped views.</summary>
    public int DisplayOrder { get; init; }

    /// <summary>Whether the lifecycle ends here, so the UI can style closed quotes without
    /// re-deriving the rule.</summary>
    public bool IsTerminal { get; init; }
}
