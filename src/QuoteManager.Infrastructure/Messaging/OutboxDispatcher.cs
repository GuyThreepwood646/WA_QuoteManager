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
/// them to <see cref="IIntegrationEventPublisher"/> — at-least-once delivery, single instance only
/// (AD-4; see Deferred for scale-out).
/// </summary>
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

            // Saved per message so a mid-batch failure doesn't re-publish an already-successful
            // sibling on the next poll.
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
