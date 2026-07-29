using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Common;
using QuoteManager.Api.Models;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Organizations;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Organizations;

public sealed record OrganizationListItem(Guid Id, string Name, string Kind, DateTimeOffset? RetiredAt);

public static class OrganizationEndpoints
{
    public static void MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/organizations", GetOrganizationsAsync);
        endpoints.MapPost("/api/organizations", CreateOrganizationAsync);
        endpoints.MapPut("/api/organizations/{organizationId:guid}", UpdateOrganizationAsync);
        endpoints.MapPost("/api/organizations/{organizationId:guid}/retire", RetireOrganizationAsync);
    }

    private static async Task<PagedResult<OrganizationListItem>> GetOrganizationsAsync(
        [AsParameters] PagedListQuery query,
        QuoteManagerDbContext db,
        CancellationToken cancellationToken,
        bool includeRetired = false)
    {
        var baseQuery = db.Organizations.AsNoTracking()
            .Where(o => includeRetired || o.RetiredAt == null)
            .OrderBy(o => o.Name)
            .Select(o => new { o.Id, o.Name, o.Kind, o.RetiredAt });

        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery.Skip(query.Skip).Take(query.ResolvedPageSize).ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new OrganizationListItem(row.Id, row.Name, row.Kind.ToString(), row.RetiredAt))
            .ToList();

        return new PagedResult<OrganizationListItem>(items, query.ResolvedPage, query.ResolvedPageSize, total);
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var trimmedName = body.Name.Trim();

        if (await db.Organizations.AsNoTracking().AnyAsync(o => o.Name == trimmedName, cancellationToken))
        {
            return DuplicateNameProblem();
        }

        var organization = Organization.Create(trimmedName, body.Kind, currentUser.ToActor(), timeProvider.GetUtcNow());
        db.Organizations.Add(organization);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Backstop for a race between two requests that both passed the AnyAsync check above -
            // the unique index is the actual guarantee, this pre-check is only the friendly path.
            return DuplicateNameProblem();
        }

        return Results.Created(
            $"/api/organizations/{organization.Id}",
            new OrganizationListItem(organization.Id, organization.Name, organization.Kind.ToString(), organization.RetiredAt));
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        Guid organizationId,
        UpdateOrganizationRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            return Results.NotFound();
        }

        var trimmedName = body.Name.Trim();

        if (await db.Organizations.AsNoTracking()
                .AnyAsync(o => o.Id != organizationId && o.Name == trimmedName, cancellationToken))
        {
            return DuplicateNameProblem();
        }

        organization.Rename(trimmedName, currentUser.ToActor(), timeProvider.GetUtcNow());

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return DuplicateNameProblem();
        }

        return Results.Ok(
            new OrganizationListItem(organization.Id, organization.Name, organization.Kind.ToString(), organization.RetiredAt));
    }

    private static async Task<IResult> RetireOrganizationAsync(
        Guid organizationId,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            return Results.NotFound();
        }

        organization.Retire(currentUser.ToActor(), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(
            new OrganizationListItem(organization.Id, organization.Name, organization.Kind.ToString(), organization.RetiredAt));
    }

    private static IResult DuplicateNameProblem() => Results.Problem(
        title: "Organization name already in use",
        detail: "Another organization already has this name.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: new Dictionary<string, object?> { ["code"] = "organization.duplicate_name" });
}
