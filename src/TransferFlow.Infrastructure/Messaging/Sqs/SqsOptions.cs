namespace TransferFlow.Infrastructure.Messaging.Sqs;

internal sealed class SqsOptions
{
    public const string SectionName = "Messaging:Sqs";

    public string QueueName { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string ServiceUrl { get; init; } = string.Empty;
}
