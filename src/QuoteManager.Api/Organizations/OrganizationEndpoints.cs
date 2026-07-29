using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Common;
using QuoteManager.Api.Models;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Organizations;

public sealed record OrganizationListItem(Guid Id, string Name, string Kind);

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/organizations", GetOrganizationsAsync);
    }

    private static async Task<PagedResult<OrganizationListItem>> GetOrganizationsAsync(
        [AsParameters] PagedListQuery query,
        QuoteManagerDbContext db,
        CancellationToken cancellationToken)
    {
        var baseQuery = db.Organizations.AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new { o.Id, o.Name, o.Kind });

        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery.Skip(query.Skip).Take(query.ResolvedPageSize).ToListAsync(cancellationToken);

        var items = rows.Select(row => new OrganizationListItem(row.Id, row.Name, row.Kind.ToString())).ToList();

        return new PagedResult<OrganizationListItem>(items, query.ResolvedPage, query.ResolvedPageSize, total);
    }
}
