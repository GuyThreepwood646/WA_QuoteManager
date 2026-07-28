using Microsoft.EntityFrameworkCore;
using QuoteManager.Domain.Organizations;
using QuoteManager.Domain.Quotes;
using QuoteManager.Domain.Requests;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Persistence.Entities;

namespace QuoteManager.Infrastructure.Persistence;

public sealed class QuoteManagerDbContext(DbContextOptions<QuoteManagerDbContext> options) : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Request> Requests => Set<Request>();

    public DbSet<Quote> Quotes => Set<Quote>();

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<RequestInvitation> RequestInvitations => Set<RequestInvitation>();

    public DbSet<QuoteStatusLookup> QuoteStatuses => Set<QuoteStatusLookup>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuoteManagerDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
