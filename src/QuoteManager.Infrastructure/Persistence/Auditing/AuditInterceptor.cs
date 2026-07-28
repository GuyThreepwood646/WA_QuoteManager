using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Persistence.Entities;

namespace QuoteManager.Infrastructure.Persistence.Auditing;

/// <summary>
/// AD-5: appends one <see cref="AuditEntry"/> per domain event, in the same
/// <see cref="QuoteManagerDbContext.SaveChangesAsync(CancellationToken)"/> call as the change that
/// raised it, so audit cannot be skipped by a code path that forgets to log.
/// </summary>
/// <remarks>
/// This is the only place that reads <see cref="AggregateRoot.DomainEvents"/> - use-case code never
/// touches an event once it has been raised. Diagnostic logging (Serilog, OpenTelemetry) is a
/// separate concern and is never the source these rows are built from.
/// </remarks>
public sealed class AuditInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is QuoteManagerDbContext context)
        {
            await AppendAuditEntriesAsync(context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task AppendAuditEntriesAsync(QuoteManagerDbContext context, CancellationToken cancellationToken)
    {
        var aggregates = context.ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var events = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();
        var actorNames = await ResolveActorNamesAsync(context, events, cancellationToken);
        var traceId = Activity.Current?.TraceId.ToString();

        foreach (var domainEvent in events)
        {
            context.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.CreateVersion7(timeProvider.GetUtcNow()),
                SubjectType = domainEvent.SubjectType,
                SubjectId = domainEvent.SubjectId,
                Action = domainEvent.Action,
                Summary = domainEvent.Summary,
                ActorId = domainEvent.ActorId,
                ActorDisplayName = actorNames.GetValueOrDefault(domainEvent.ActorId, "Unknown"),
                OccurredAt = domainEvent.OccurredAt,
                TraceId = traceId,
            });
        }

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }
    }

    /// <summary>
    /// Resolves the display name behind each acting id, including ids for <see cref="AppUser"/>
    /// rows created earlier in this same unit of work - the seeder writes users and the requests
    /// they act on in one <c>SaveChangesAsync</c> call, so those rows are not queryable yet.
    /// </summary>
    private static async Task<Dictionary<Guid, string>> ResolveActorNamesAsync(
        QuoteManagerDbContext context,
        IReadOnlyCollection<IDomainEvent> events,
        CancellationToken cancellationToken)
    {
        var actorIds = events.Select(e => e.ActorId).Distinct().ToList();
        var names = new Dictionary<Guid, string> { [DomainActor.System.Id] = DomainActor.System.DisplayName };

        foreach (var entry in context.ChangeTracker.Entries<AppUser>())
        {
            if (actorIds.Contains(entry.Entity.Id))
            {
                names[entry.Entity.Id] = entry.Entity.DisplayName;
            }
        }

        var unresolved = actorIds.Where(id => !names.ContainsKey(id)).ToList();
        if (unresolved.Count > 0)
        {
            var fromDatabase = await context.Users.AsNoTracking()
                .Where(user => unresolved.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName })
                .ToListAsync(cancellationToken);

            foreach (var user in fromDatabase)
            {
                names[user.Id] = user.DisplayName;
            }
        }

        return names;
    }
}
