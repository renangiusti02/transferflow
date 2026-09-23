using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Options;
using System.Globalization;
using TransferFlow.Application.Messaging.Projections;
using TransferFlow.Infrastructure.Messaging.Projections.DynamoDb;

namespace TransferFlow.Integration.Tests.DynamoDb;

public sealed class DynamoDbWalletActivityProjectionTests
{
    private const string TableName = "wallet-activity";

    [Fact]
    [Trait("Category", "LocalStack")]
    public async Task Upsert_And_Get_Recent_Should_Be_Idempotent_And_Return_Most_Recent_First()
    {
        using var client =
            new AmazonDynamoDBClient(
                new BasicAWSCredentials("test", "test"),
                new AmazonDynamoDBConfig
                {
                    ServiceURL = "http://localhost:4566",
                    AuthenticationRegion = "us-east-1"
                });

        var options =
            Options.Create(
                new DynamoDbProjectionOptions
                {
                    TableName = TableName,
                    Region = "us-east-1",
                    ServiceUrl = "http://localhost:4566"
                });

        var projection =
            new DynamoDbWalletActivityProjection(
                client,
                options);

        var walletId = Guid.NewGuid();
        var counterpartyWalletId = Guid.NewGuid();

        var olderActivity =
            new WalletActivity(
                Guid.NewGuid(),
                walletId,
                counterpartyWalletId,
                30m,
                WalletActivityDirection.Debit,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                Guid.NewGuid());

        var newerActivity =
            new WalletActivity(
                Guid.NewGuid(),
                walletId,
                counterpartyWalletId,
                20m,
                WalletActivityDirection.Credit,
                DateTimeOffset.UtcNow,
                Guid.NewGuid());

        try
        {
            await projection.UpsertAsync(
                olderActivity,
                CancellationToken.None);

            // Same logical projection item again.
            await projection.UpsertAsync(
                olderActivity,
                CancellationToken.None);

            await projection.UpsertAsync(
                newerActivity,
                CancellationToken.None);

            var activities =
                await projection.GetRecentAsync(
                    walletId,
                    10,
                    CancellationToken.None);

            Assert.Equal(2, activities.Count);

            Assert.Equal(
                newerActivity.TransferId,
                activities[0].TransferId);

            Assert.Equal(
                olderActivity.TransferId,
                activities[1].TransferId);
        }
        finally
        {
            await DeleteAsync(
                client,
                olderActivity);

            await DeleteAsync(
                client,
                newerActivity);
        }
    }

    private static Task<DeleteItemResponse> DeleteAsync(
        AmazonDynamoDBClient client,
        WalletActivity activity)
    {
        var occurredAtUtc =
            activity.OccurredAtUtc
                .UtcDateTime
                .ToString(
                    "O",
                    CultureInfo.InvariantCulture);

        var request =
            new DeleteItemRequest
            {
                TableName = TableName,

                Key =
                    new Dictionary<string, AttributeValue>
                    {
                        ["pk"] = new()
                        {
                            S = $"WALLET#{activity.WalletId}"
                        },

                        ["sk"] = new()
                        {
                            S =
                                $"ACTIVITY#{occurredAtUtc}#{activity.TransferId}"
                        }
                    }
            };

        return client.DeleteItemAsync(
            request,
            CancellationToken.None);
    }
}
