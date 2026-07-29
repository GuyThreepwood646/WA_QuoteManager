using Microsoft.EntityFrameworkCore;
using QuoteManager.Api.Common;
using QuoteManager.Api.Models;
using QuoteManager.Application.Abstractions;
using QuoteManager.Domain.Organizations;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Api.Organizations;

public sealed record OrganizationLocationItem(Guid Id, string Address, string? Phone, int SortOrder);

public sealed record OrganizationListItem(
    Guid Id,
    string Name,
    string Kind,
    DateTimeOffset? RetiredAt,
    string? PrimaryAddress,
    string? PrimaryContactName,
    string? PrimaryContactEmail,
    string? PrimaryContactPhone,
    bool IsPreferredVendor,
    IReadOnlyList<OrganizationLocationItem> Locations);

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
            .Include(o => o.Locations)
            .Where(o => includeRetired || o.RetiredAt == null)
            .OrderBy(o => o.Name);

        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery.Skip(query.Skip).Take(query.ResolvedPageSize).ToListAsync(cancellationToken);

        var items = rows.Select(ToListItem).ToList();

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
        organization.UpdateProfile(
            trimmedName,
            body.PrimaryAddress,
            body.PrimaryContactName,
            body.PrimaryContactEmail,
            body.PrimaryContactPhone,
            body.IsPreferredVendor,
            ToLocationInputs(body.Locations),
            currentUser.ToActor(),
            timeProvider.GetUtcNow());

        db.Organizations.Add(organization);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateOrganizationName(ex))
        {
            return DuplicateNameProblem();
        }

        return Results.Created($"/api/organizations/{organization.Id}", ToListItem(organization));
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        Guid organizationId,
        UpdateOrganizationRequest body,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var organization = await db.Organizations
            .Include(o => o.Locations)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
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

        if (body.IsPreferredVendor && organization.Kind != OrganizationKind.Vendor)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(body.IsPreferredVendor)] = ["Only vendor organizations can be marked as preferred."],
            });
        }

        organization.UpdateProfile(
            trimmedName,
            body.PrimaryAddress,
            body.PrimaryContactName,
            body.PrimaryContactEmail,
            body.PrimaryContactPhone,
            body.IsPreferredVendor,
            ToLocationInputs(body.Locations),
            currentUser.ToActor(),
            timeProvider.GetUtcNow());

        await db.EnsureNewLocationsAreAddedAsync(organization, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateOrganizationName(ex))
        {
            return DuplicateNameProblem();
        }

        return Results.Ok(ToListItem(organization));
    }

    private static async Task<IResult> RetireOrganizationAsync(
        Guid organizationId,
        QuoteManagerDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var organization = await db.Organizations
            .Include(o => o.Locations)
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);
        if (organization is null)
        {
            return Results.NotFound();
        }

        organization.Retire(currentUser.ToActor(), timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToListItem(organization));
    }

    private static OrganizationListItem ToListItem(Organization organization) =>
        new(
            organization.Id,
            organization.Name,
            organization.Kind.ToString(),
            organization.RetiredAt,
            organization.PrimaryAddress,
            organization.PrimaryContactName,
            organization.PrimaryContactEmail,
            organization.PrimaryContactPhone,
            organization.IsPreferredVendor,
            organization.Locations
                .OrderBy(l => l.SortOrder)
                .Select(l => new OrganizationLocationItem(l.Id, l.Address, l.Phone, l.SortOrder))
                .ToList());

    private static IEnumerable<OrganizationLocationInput> ToLocationInputs(IEnumerable<OrganizationLocationRequest> locations) =>
        locations.Select(location => new OrganizationLocationInput(location.Address, location.Phone));

    private static bool IsDuplicateOrganizationName(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            && message.Contains("Organizations", StringComparison.OrdinalIgnoreCase);
    }

    private static IResult DuplicateNameProblem() => Results.Problem(
        title: "Organization name already in use",
        detail: "Another organization already has this name.",
        statusCode: StatusCodes.Status409Conflict,
        extensions: new Dictionary<string, object?> { ["code"] = "organization.duplicate_name" });
}
