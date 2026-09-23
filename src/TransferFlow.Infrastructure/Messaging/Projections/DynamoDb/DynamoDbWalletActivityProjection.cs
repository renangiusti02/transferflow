using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using System.Globalization;
using TransferFlow.Application.Messaging.Projections;

namespace TransferFlow.Infrastructure.Messaging.Projections.DynamoDb;

internal sealed class DynamoDbWalletActivityProjection(
    IAmazonDynamoDB dynamoDb,
    IOptions<DynamoDbProjectionOptions> options)
    : IWalletActivityProjection
{
    private readonly IAmazonDynamoDB _dynamoDb = dynamoDb;
    private readonly DynamoDbProjectionOptions _options = options.Value;

    public async Task UpsertAsync(
        WalletActivity activity,
        CancellationToken cancellationToken)
    {
        var occurredAtUtc =
            activity.OccurredAtUtc
                .UtcDateTime
                .ToString("O", CultureInfo.InvariantCulture);

        var request = new PutItemRequest
        {
            TableName = _options.TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new()
                {
                    S = $"WALLET#{activity.WalletId}"
                },

                ["sk"] = new()
                {
                    S = $"ACTIVITY#{occurredAtUtc}#{activity.TransferId}"
                },

                ["transferId"] = new()
                {
                    S = activity.TransferId.ToString()
                },

                ["walletId"] = new()
                {
                    S = activity.WalletId.ToString()
                },

                ["counterpartyWalletId"] = new()
                {
                    S = activity.CounterpartyWalletId.ToString()
                },

                ["amount"] = new()
                {
                    N = activity.Amount.ToString(CultureInfo.InvariantCulture)
                },

                ["direction"] = new()
                {
                    S = activity.Direction.ToString()
                },

                ["correlationId"] = new()
                {
                    S = activity.CorrelationId.ToString()
                },

                ["occurredAtUtc"] = new()
                {
                    S = occurredAtUtc
                }
            }
        };

        await _dynamoDb.PutItemAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<WalletActivity>> GetRecentAsync(
        Guid walletId,
        int limit,
        CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            TableName = _options.TableName,
            KeyConditionExpression = "pk = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new()
                    {
                        S = $"WALLET#{walletId}"
                    }
                },
            Limit = limit,
            ScanIndexForward = false // Get the most recent items first
        };

        var response = await _dynamoDb.QueryAsync(request, cancellationToken);
        return [.. response.Items.Select(ToWalletActivity)];
    }

    private static WalletActivity ToWalletActivity(
        Dictionary<string, AttributeValue> item)
    {
        return new WalletActivity(
            Guid.Parse(item["transferId"].S),
            Guid.Parse(item["walletId"].S),
            Guid.Parse(item["counterpartyWalletId"].S),
            decimal.Parse(
                item["amount"].N,
                CultureInfo.InvariantCulture),
            Enum.Parse<WalletActivityDirection>(
                item["direction"].S),
            DateTimeOffset.Parse(
                item["occurredAtUtc"].S,
                CultureInfo.InvariantCulture),
            Guid.Parse(item["correlationId"].S));
    }
}
