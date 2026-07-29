using System.ComponentModel.DataAnnotations;

namespace QuoteManager.Api.Models;

/// <summary>
/// The one list-query shape every list endpoint binds via <c>[AsParameters]</c>:
/// <c>?page=1&amp;pageSize=25</c>.
/// </summary>
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
