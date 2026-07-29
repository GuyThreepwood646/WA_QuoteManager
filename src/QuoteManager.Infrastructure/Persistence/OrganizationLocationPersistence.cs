using Microsoft.EntityFrameworkCore;
using QuoteManager.Domain.Organizations;

namespace QuoteManager.Infrastructure.Persistence;

public static class OrganizationLocationPersistence
{
    /// <summary>
    /// EF can mark brand-new child locations as <see cref="EntityState.Modified"/> when they are
    /// appended to a field-backed aggregate collection that was loaded in a prior unit of work.
    /// </summary>
    public static async Task EnsureNewLocationsAreAddedAsync(
        this QuoteManagerDbContext db,
        Organization organization,
        CancellationToken cancellationToken)
    {
        var persistedLocationIds = await db.Set<OrganizationLocation>()
            .AsNoTracking()
            .Where(location => location.OrganizationId == organization.Id)
            .Select(location => location.Id)
            .ToListAsync(cancellationToken);

        foreach (var entry in db.ChangeTracker.Entries<OrganizationLocation>()
            .Where(entry => entry.Entity.OrganizationId == organization.Id)
            .Where(entry => !persistedLocationIds.Contains(entry.Entity.Id)))
        {
            entry.State = EntityState.Added;
        }
    }
}
