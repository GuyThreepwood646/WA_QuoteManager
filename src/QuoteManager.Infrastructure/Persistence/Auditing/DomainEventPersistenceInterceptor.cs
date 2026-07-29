using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuoteManager.Application.Messaging;
using QuoteManager.Domain.Common;
using QuoteManager.Domain.Identity;
using QuoteManager.Infrastructure.Identity;
using QuoteManager.Infrastructure.Persistence.Entities;

namespace QuoteManager.Infrastructure.Persistence.Auditing;

/// <summary>
/// On every <see cref="QuoteManagerDbContext.SaveChangesAsync(CancellationToken)"/>, appends one
/// <see cref="AuditEntry"/> per domain event and, for events on the integration allow-list, one
/// <see cref="OutboxMessage"/> - both inside the same transaction as the change that raised them.
/// </summary>
/// <remarks>
/// Combined into one interceptor deliberately, not split across two independently registered
/// ones: both passes need to read the exact same in-memory event list before it is cleared, and
/// relying on EF Core's interceptor invocation order for two separate classes to agree on who
/// clears first would be exactly the kind of implicit coupling this design otherwise avoids.
/// Translation itself still stays delegated - this class only reads once, hands each event to
/// <see cref="IntegrationEventAllowList.Resolve"/>, and clears once. This is the only place that
/// reads <see cref="AggregateRoot.DomainEvents"/> - use-case code never touches an event once it
/// has been raised. Diagnostic logging (Serilog, OpenTelemetry) is a separate concern and is never
/// the source either of these tables is built from.
/// </remarks>
public sealed class DomainEventPersistenceInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is QuoteManagerDbContext context)
        {
            await AppendAuditAndOutboxEntriesAsync(context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task AppendAuditAndOutboxEntriesAsync(QuoteManagerDbContext context, CancellationToken cancellationToken)
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

        // Request.ApplyQuoteAction can raise several events from one call (Accept, then a Reject per
        // superseded sibling, then RequestAwarded) that share one DateTimeOffset, so minting each id from
        // a fresh timeProvider.GetUtcNow() risks ties or even reversed order once two calls land in the
        // same clock tick - silently breaking both the timeline's newest-first sort and the
        // OutboxDispatcher's OrderBy(m => m.Id) delivery order. Guid.CreateVersion7 only encodes
        // millisecond resolution (RFC 9562's 48-bit unix_ts_ms), so the offset must advance by whole
        // milliseconds - not ticks - to actually change the timestamp field each id sorts on.
        var idClock = timeProvider.GetUtcNow();
        var nextIdOffsetMs = 0;

        foreach (var domainEvent in events)
        {
            context.AuditEntries.Add(new AuditEntry
            {
                Id = Guid.CreateVersion7(idClock.AddMilliseconds(nextIdOffsetMs++)),
                SubjectType = domainEvent.SubjectType,
                SubjectId = domainEvent.SubjectId,
                Action = domainEvent.Action,
                Summary = domainEvent.Summary,
                ActorId = domainEvent.ActorId,
                ActorDisplayName = actorNames.GetValueOrDefault(domainEvent.ActorId, "Unknown"),
                OccurredAt = domainEvent.OccurredAt,
                TraceId = traceId,
            });

            if (IntegrationEventAllowList.Resolve(domainEvent) is { } integrationEvent)
            {
                context.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.CreateVersion7(idClock.AddMilliseconds(nextIdOffsetMs++)),
                    Type = integrationEvent.ContractName,
                    Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
                    OccurredAt = domainEvent.OccurredAt,
                });
            }
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
