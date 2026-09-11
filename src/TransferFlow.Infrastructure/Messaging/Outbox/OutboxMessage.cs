namespace TransferFlow.Infrastructure.Messaging.Outbox;

internal sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(string type,
                         string payload,
                         DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException(
                "Message type is required.",
                nameof(type));
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException(
                "Message payload is required.",
                nameof(payload));
        }

        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        OccurredAtUtc = occurredAtUtc;
    }

    public void MarkAsProcessed(
        DateTimeOffset processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
    }
}
