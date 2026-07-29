namespace QuoteManager.Api.Common;

/// <summary>
/// The one envelope every list endpoint returns: collections are never a bare JSON array.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
