using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The one list-query shape every list endpoint binds via <c>[AsParameters]</c>:
/// <c>?page=1&amp;pageSize=25</c>.
/// </summary>
/// <remarks>
/// <see cref="Page"/> and <see cref="PageSize"/> are nullable so an absent query parameter binds
/// to "no preference", not "an invalid value" - minimal API's <c>[AsParameters]</c> binder marks a
/// non-nullable property as a required query value, which would turn a plain
/// <c>GET /api/requests</c> with no query string into a 400. <see cref="ResolvedPage"/> defaults
/// an absent page to 1; <see cref="ResolvedPageSize"/> defaults an absent size to 25 and clamps
/// anything above 100, since a caller asking for too much is a performance concern, not a client
/// error - that clamp is deliberately not duplicated as a rejection in <see cref="Validate"/>.
/// <see cref="Validate"/> instead rejects only the values clamping has no sensible answer for: an
/// explicit page or page size that is zero or negative, which the previous hand-rolled
/// <c>PagedQuery</c> used to silently reinterpret as "use the default" - hiding a caller's bug
/// rather than reporting it.
/// </remarks>
public sealed record PagedListQuery : IValidatableObject
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public int ResolvedPage => Page ?? 1;

    public int ResolvedPageSize => Math.Clamp(PageSize ?? DefaultPageSize, 1, MaxPageSize);

    public int Skip => (ResolvedPage - 1) * ResolvedPageSize;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Page is < 1)
        {
            yield return new ValidationResult("page must be 1 or greater.", [nameof(Page)]);
        }

        if (PageSize is <= 0)
        {
            yield return new ValidationResult("pageSize must be greater than zero.", [nameof(PageSize)]);
        }
    }
}
