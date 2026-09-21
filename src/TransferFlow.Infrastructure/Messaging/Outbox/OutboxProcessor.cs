using Microsoft.EntityFrameworkCore;
using TransferFlow.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace TransferFlow.Infrastructure.Messaging.Outbox;

internal sealed class OutboxProcessor(
    TransferFlowDbContext dbContext,
    IOutboxPublisher publisher,
    ILogger<OutboxProcessor> logger)
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
            using var logScope =
                logger.BeginScope(
                    new Dictionary<string, object?>
                    {
                        ["CorrelationId"] = message.CorrelationId,
                        ["OutboxMessageId"] = message.Id,
                        ["EventType"] = message.Type
                    });

            try
            {
                logger.LogInformation(
                    "Publishing outbox message.");

                await _publisher.PublishAsync(
                    message,
                    cancellationToken);

                message.MarkAsProcessed(
                    DateTimeOffset.UtcNow);

                logger.LogInformation(
                    "Outbox message published successfully.");
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish outbox message.");
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
