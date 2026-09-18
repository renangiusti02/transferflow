internal sealed class ProcessedMessage
{
    public Guid MessageId { get; private set; }
    public DateTimeOffset ProcessedAtUtc { get; private set; }

    private ProcessedMessage()
    {
    }

    public ProcessedMessage(
        Guid messageId,
        DateTimeOffset processedAtUtc)
    {
        MessageId = messageId;
        ProcessedAtUtc = processedAtUtc;
    }
}
