namespace QuoteManager.Api.Common;

/// <summary>
/// The one envelope every list endpoint returns: collections are never a bare JSON array.
/// </summary>
/// <remarks>
/// The request-side counterpart, <c>PagedListQuery</c>, lives in <c>Api/Models</c> alongside every
/// other bound input, since it is itself validated via <c>IValidatableObject</c>.
/// </remarks>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
