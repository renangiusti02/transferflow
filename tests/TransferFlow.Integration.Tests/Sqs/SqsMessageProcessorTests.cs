using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using TransferFlow.Application.Messaging.Events;
using TransferFlow.Application.Messaging.Projections;
using TransferFlow.Infrastructure.Messaging.Sqs;
using TransferFlow.Infrastructure.Persistence;
using TransferFlow.Integration.Tests.Infrastructure;

namespace TransferFlow.Integration.Tests.Sqs;

[Collection("PostgreSQL integration tests")]
public sealed class SqsMessageProcessorTests(
    PostgreSqlIntegrationTestFixture database) : IClassFixture<PostgreSqlIntegrationTestFixture>
{
    private readonly PostgreSqlIntegrationTestFixture _database = database;

    internal sealed class FakeWalletActivityProjection
    : IWalletActivityProjection
    {
        private readonly Dictionary<string, WalletActivity> _activities = [];

        public Task UpsertAsync(
            WalletActivity activity,
            CancellationToken cancellationToken)
        {
            var key = $"{activity.WalletId}:{activity.OccurredAtUtc:O}:{activity.TransferId}";

            _activities[key] = activity;

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WalletActivity>> GetRecentAsync(
            Guid walletId,
            int limit,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<WalletActivity> result = [
                ..
                _activities.Values
                    .Where(x => x.WalletId == walletId)
                    .OrderByDescending(x => x.OccurredAtUtc)
                    .Take(limit)
            ];

            return Task.FromResult(result);
        }

        public IReadOnlyCollection<WalletActivity> Activities =>
            _activities.Values;
    }

    private sealed class FailingSaveChangesInterceptor
    : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>>
            SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException<InterceptionResult<int>>(
                new InvalidOperationException(
                    "Simulated database commit failure."));
        }
    }

    [Fact]
    public async Task Sqs_Process_Async_Should_Persist_One_Processed_Message_When_Same_Message_Is_Delivered_Twice()
    {
        var projection = new FakeWalletActivityProjection();
        var applicationMessageId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        var integrationEvent =
            new TransferCompleted(
                transferId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                100m,
                DateTimeOffset.UtcNow,
            Guid.NewGuid());

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            Body = JsonSerializer.Serialize(integrationEvent),

            MessageAttributes =
                new Dictionary<string, MessageAttributeValue>
                {
                    ["message-id"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = applicationMessageId.ToString()
                    },

                    ["event-type"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = nameof(TransferCompleted)
                    },

                    ["correlation-id"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = transferId.ToString()
                    }
                }
        };

        try
        {
            await using (var dbContext = _database.CreateDbContext())
            {
                var processor =
                    new SqsMessageProcessor(dbContext, projection);

                await processor.ProcessAsync(
                    message,
                    CancellationToken.None);
            }

            var firstSqsMessageId =
                message.MessageId;

            message.MessageId =
                Guid.NewGuid().ToString();

            Assert.NotEqual(
                firstSqsMessageId,
                message.MessageId);

            await using (var dbContext = _database.CreateDbContext())
            {
                var processor =
                    new SqsMessageProcessor(dbContext, projection);

                await processor.ProcessAsync(
                    message,
                    CancellationToken.None);
            }

            await using var verificationDbContext =
                _database.CreateDbContext();

            var count =
                await verificationDbContext
                    .Set<ProcessedMessage>()
                    .CountAsync(x =>
                        x.MessageId ==
                        applicationMessageId);

            Assert.Equal(1, count);

            Assert.Equal(2, projection.Activities.Count);

            Assert.Contains(
                projection.Activities,
                x =>
                    x.WalletId == integrationEvent.SourceWalletId &&
                    x.Direction == WalletActivityDirection.Debit);

            Assert.Contains(
                projection.Activities,
                x =>
                    x.WalletId == integrationEvent.DestinationWalletId &&
                    x.Direction == WalletActivityDirection.Credit);
        }
        finally
        {
            await using var cleanupDbContext =
                _database.CreateDbContext();

            await cleanupDbContext
                .Set<ProcessedMessage>()
                .Where(x =>
                    x.MessageId ==
                    applicationMessageId)
                .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Sqs_Process_Async_Should_Succeed_On_Redelivery_After_Transient_Database_Failure()
    {
        var projection = new FakeWalletActivityProjection();
        var applicationMessageId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        var integrationEvent =
            new TransferCompleted(
                transferId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                100m,
                DateTimeOffset.UtcNow,
                Guid.NewGuid());

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            Body = JsonSerializer.Serialize(integrationEvent),

            MessageAttributes =
                new Dictionary<string, MessageAttributeValue>
                {
                    ["message-id"] = new()
                    {
                        DataType = "String",
                        StringValue =
                            applicationMessageId.ToString()
                    },

                    ["event-type"] = new()
                    {
                        DataType = "String",
                        StringValue =
                            nameof(TransferCompleted)
                    },

                    ["correlation-id"] = new()
                    {
                        DataType = "String",
                        StringValue =
                            transferId.ToString()
                    }
                }
        };

        try
        {
            var failingOptions =
                new DbContextOptionsBuilder<TransferFlowDbContext>()
                    .UseNpgsql(_database.ConnectionString)
                    .AddInterceptors(
                        new FailingSaveChangesInterceptor())
                    .Options;

            await using (var failingDbContext =
                new TransferFlowDbContext(failingOptions))
            {
                var processor =
                    new SqsMessageProcessor(
                        failingDbContext,
                        projection);

                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    processor.ProcessAsync(
                        message,
                        CancellationToken.None));
            }

            Assert.Equal(2, projection.Activities.Count);

            await using (var beforeRedeliveryVerificationDbContext =
                _database.CreateDbContext())
            {
                var beforeRedeliveryCount =
                    await beforeRedeliveryVerificationDbContext
                        .Set<ProcessedMessage>()
                        .CountAsync(x =>
                            x.MessageId == applicationMessageId);

                Assert.Equal(0, beforeRedeliveryCount);
            }

            message.MessageId =
                Guid.NewGuid().ToString();

            await using (var dbContext =
                _database.CreateDbContext())
            {
                var processor =
                    new SqsMessageProcessor(dbContext, projection);

                await processor.ProcessAsync(
                    message,
                    CancellationToken.None);
            }

            Assert.Equal(2, projection.Activities.Count);

            await using (var afterRedeliveryVerificationDbContext =
                _database.CreateDbContext())
            {
                var afterRedeliveryCount =
                    await afterRedeliveryVerificationDbContext
                        .Set<ProcessedMessage>()
                        .CountAsync(x =>
                            x.MessageId == applicationMessageId);

                Assert.Equal(1, afterRedeliveryCount);
            }

            Assert.Contains(
                projection.Activities,
                x =>
                    x.WalletId == integrationEvent.SourceWalletId &&
                    x.Direction == WalletActivityDirection.Debit);

            Assert.Contains(
                projection.Activities,
                x =>
                    x.WalletId == integrationEvent.DestinationWalletId &&
                    x.Direction == WalletActivityDirection.Credit);
        }
        finally
        {
            await using var cleanupDbContext =
                _database.CreateDbContext();

            await cleanupDbContext
                .Set<ProcessedMessage>()
                .Where(x =>
                    x.MessageId ==
                    applicationMessageId)
                .ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Sqs_Process_Async_Should_Not_Persist_Message_When_EventType_Is_Unsupported()
    {
        var projection = new FakeWalletActivityProjection();
        var applicationMessageId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        var integrationEvent =
            new TransferCompleted(
                transferId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                100m,
                DateTimeOffset.UtcNow,
                Guid.NewGuid());

        var message = new Message
        {
            MessageId = Guid.NewGuid().ToString(),
            Body = JsonSerializer.Serialize(integrationEvent),

            MessageAttributes =
                new Dictionary<string, MessageAttributeValue>
                {
                    ["message-id"] = new()
                    {
                        DataType = "String",
                        StringValue =
                            applicationMessageId.ToString()
                    },

                    ["event-type"] = new()
                    {
                        DataType = "String",
                        StringValue = "UnsupportedEventType"
                    },

                    ["correlation-id"] = new()
                    {
                        DataType = "String",
                        StringValue =
                            transferId.ToString()
                    }
                }
        };

        try
        {
            await using (var dbContext = _database.CreateDbContext())
            {
                var processor =
                    new SqsMessageProcessor(dbContext, projection);

                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    processor.ProcessAsync(
                        message,
                        CancellationToken.None));
            }

            await using var verificationDbContext =
                _database.CreateDbContext();

            var count =
                await verificationDbContext
                    .Set<ProcessedMessage>()
                    .CountAsync(x =>
                        x.MessageId ==
                        applicationMessageId);

            Assert.Equal(0, count);
        }
        finally
        {
            await using var cleanupDbContext =
                _database.CreateDbContext();

            await cleanupDbContext
                .Set<ProcessedMessage>()
                .Where(x =>
                    x.MessageId ==
                    applicationMessageId)
                .ExecuteDeleteAsync();
        }
    }
}
