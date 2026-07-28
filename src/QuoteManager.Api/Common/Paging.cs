namespace QuoteManager.Api.Common;

/// <summary>
/// The one list-query shape every list endpoint binds: <c>?page=1&amp;pageSize=25</c>.
/// </summary>
/// <remarks>
/// <c>page</c> is 1-based. <c>pageSize</c> defaults to 25 and is clamped to 100 rather than
/// rejected, since a caller asking for too much is a performance concern, not a client error.
/// Endpoints accept <c>int? page, int? pageSize</c> directly rather than binding this type via
/// <c>[AsParameters]</c>: minimal API's parameter-object binder treats non-nullable properties as
/// required query values, which would make an absent <c>?page=</c> a 400 instead of "use the
/// default" - the opposite of the clamp-don't-reject rule this type exists to enforce.
/// </remarks>
public sealed class PagedQuery
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    public PagedQuery(int? page, int? pageSize)
    {
        Page = page is null or < 1 ? 1 : page.Value;
        PageSize = Math.Clamp(pageSize is null or <= 0 ? DefaultPageSize : pageSize.Value, 1, MaxPageSize);
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}

/// <summary>
/// The one envelope every list endpoint returns: collections are never a bare JSON array.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
