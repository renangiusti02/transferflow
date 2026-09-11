namespace TransferFlow.Infrastructure.Messaging.Outbox;

internal interface IOutboxPublisher
{
    Task PublishAsync(
        OutboxMessage message,
        CancellationToken cancellationToken);
}
