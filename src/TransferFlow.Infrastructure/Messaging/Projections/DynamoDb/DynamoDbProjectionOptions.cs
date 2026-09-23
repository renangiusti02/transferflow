namespace TransferFlow.Infrastructure.Messaging.Projections.DynamoDb;

internal sealed class DynamoDbProjectionOptions
{
    public const string SectionName = "Projections:DynamoDb";
    public string TableName { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string ServiceUrl { get; init; } = string.Empty;
}
