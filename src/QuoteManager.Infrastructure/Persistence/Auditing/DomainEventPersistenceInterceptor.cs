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
/// <see cref="AuditEntry"/> per domain event and, for allow-listed events, one
/// <see cref="OutboxMessage"/> — both inside the change's own transaction (AD-4, AD-5).
/// </summary>
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

        // Events raised in one call share one DateTimeOffset, so ids are minted from a single clock
        // reading advanced by whole milliseconds each — Guid.CreateVersion7 only encodes millisecond
        // resolution, and ties would scramble both the timeline sort and dispatch order (AD-5).
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
                Note = domainEvent.Note,
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
    /// Resolves the display name behind each acting id, including <see cref="AppUser"/> rows
    /// created earlier in the same unit of work (not yet queryable) — the seeder writes users and
    /// the requests they act on in one <c>SaveChangesAsync</c> call.
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
