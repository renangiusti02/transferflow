using Microsoft.EntityFrameworkCore;
using TransferFlow.Infrastructure.Persistence;

namespace TransferFlow.Infrastructure.Messaging.Outbox;

internal sealed class OutboxProcessor(TransferFlowDbContext dbContext, IOutboxPublisher publisher)
{
    private readonly TransferFlowDbContext _dbContext = dbContext;
    private readonly IOutboxPublisher _publisher = publisher;

    public async Task ProcessPendingAsync(
        CancellationToken cancellationToken)
    {
        var pendingMessages =
            await _dbContext
                .Set<OutboxMessage>()
                .Where(message => message.ProcessedAtUtc == null)
                .OrderBy(message => message.OccurredAtUtc)
                .Take(20)
                .ToListAsync(cancellationToken);

        foreach (var message in pendingMessages)
        {
            try
            {
                await _publisher.PublishAsync(
                    message,
                    cancellationToken);

                message.MarkAsProcessed(
                    DateTimeOffset.UtcNow);
            }
            catch (Exception)
                when (!cancellationToken.IsCancellationRequested)
            {
                // Log the exception and continue processing the next message
                // Does not mark the message as processed, so it will be retried in the next run
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
