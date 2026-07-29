using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuoteManager.Application.Abstractions;
using QuoteManager.Application.Messaging;
using QuoteManager.Infrastructure.Persistence;

namespace QuoteManager.Infrastructure.Messaging;

/// <summary>
/// The only component that reads <see cref="Persistence.Entities.OutboxMessage"/> rows and hands
/// them to <see cref="IIntegrationEventPublisher"/>.
/// </summary>
/// <remarks>
/// Claims undispatched rows in insertion order - <c>OrderBy(m =&gt; m.Id)</c> is sufficient because
/// every id is a UUIDv7, which sorts monotonically by creation time. A message is marked
/// dispatched only after <see cref="IIntegrationEventPublisher.PublishAsync"/> returns without
/// throwing, so delivery is at-least-once: a publish that succeeds but whose <c>DispatchedAt</c>
/// write is then lost to a crash is redelivered on the next poll. Every consumer must therefore be
/// idempotent, keyed on <see cref="IntegrationEventEnvelope.Id"/>. This drains a single instance in
/// order; scaling out to multiple dispatchers would need a claim mechanism this doesn't have.
/// </remarks>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MessagingLog.DispatchLoopFailed(logger, ex);
            }

            try
            {
                await Task.Delay(PollInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuoteManagerDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        var pending = await db.OutboxMessages
            .Where(m => m.DispatchedAt == null)
            .OrderBy(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            var envelope = new IntegrationEventEnvelope(message.Id, message.Type, message.Payload, message.OccurredAt);

            try
            {
                await publisher.PublishAsync(envelope, cancellationToken);
                message.DispatchedAt = timeProvider.GetUtcNow();
                message.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message;
                MessagingLog.PublishFailed(logger, ex, message.Id, message.Type, message.Attempts);
            }

            // Saved per message, not once per batch: if the Nth message's publish throws, the
            // first N-1 successes must still be durably marked dispatched rather than re-published
            // on the next poll just because they happened to share a batch with a failure.
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
