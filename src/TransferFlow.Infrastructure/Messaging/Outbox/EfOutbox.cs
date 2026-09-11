using System.Text.Json;
using TransferFlow.Application.Messaging;
using TransferFlow.Infrastructure.Persistence;

namespace TransferFlow.Infrastructure.Messaging.Outbox;

public sealed class EfOutbox(TransferFlowDbContext dbContext) : IOutbox
{
    private readonly TransferFlowDbContext _dbContext = dbContext;

    public void Add<T>(
        T message)
        where T : IIntegrationEvent
    {
        var type = typeof(T).Name;
        var payload = JsonSerializer.Serialize(message);

        var outboxMessage = new OutboxMessage(
            type,
            payload,
            message.OccurredAtUtc);

        _dbContext.Set<OutboxMessage>()
            .Add(outboxMessage);
    }
}
