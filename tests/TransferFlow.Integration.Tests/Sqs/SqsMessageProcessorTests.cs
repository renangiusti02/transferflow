using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using TransferFlow.Application.Messaging.Events;
using TransferFlow.Infrastructure.Messaging.Sqs;
using TransferFlow.Infrastructure.Persistence;

namespace TransferFlow.Integration.Tests.Sqs;

[Collection("PostgreSQL integration tests")]
public sealed class SqsMessageProcessorTests
{
    private readonly string _connectionString;

    public SqsMessageProcessorTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<SqsMessageProcessorTests>()
            .Build();

        _connectionString =
            configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'Database' was not found.");
    }

    private TransferFlowDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<TransferFlowDbContext>()
                .UseNpgsql(_connectionString)
                .Options;

        return new TransferFlowDbContext(options);
    }

    [Fact]
    public async Task Sqs_Process_Async_Should_Persist_One_Processed_Message_When_Same_Message_Is_Delivered_Twice()
    {
        var applicationMessageId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        var integrationEvent =
            new TransferCompleted(
                transferId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                100m,
                DateTimeOffset.UtcNow);

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
            await using (var dbContext = CreateDbContext())
            {
                var processor =
                    new SqsMessageProcessor(dbContext);

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

            await using (var dbContext = CreateDbContext())
            {
                var processor =
                    new SqsMessageProcessor(dbContext);

                await processor.ProcessAsync(
                    message,
                    CancellationToken.None);
            }

            await using var verificationDbContext =
                CreateDbContext();

            var count =
                await verificationDbContext
                    .Set<ProcessedMessage>()
                    .CountAsync(x =>
                        x.MessageId ==
                        applicationMessageId);

            Assert.Equal(1, count);
        }
        finally
        {
            await using var cleanupDbContext =
                CreateDbContext();

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
        var applicationMessageId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        var integrationEvent =
            new TransferCompleted(
                transferId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                100m,
                DateTimeOffset.UtcNow);

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
            var brokenConnectionString =
                new Npgsql.NpgsqlConnectionStringBuilder(
                    _connectionString)
                {
                    Port = 1,
                    Timeout = 1
                }
                .ConnectionString;

            var brokenOptions =
                new DbContextOptionsBuilder<TransferFlowDbContext>()
                    .UseNpgsql(brokenConnectionString)
                    .Options;

            await using (var brokenDbContext =
                new TransferFlowDbContext(brokenOptions))
            {
                var processor =
                    new SqsMessageProcessor(brokenDbContext);

                await Assert.ThrowsAnyAsync<Exception>(() =>
                    processor.ProcessAsync(
                        message,
                        CancellationToken.None));
            }

            message.MessageId =
                Guid.NewGuid().ToString();

            await using (var dbContext =
                CreateDbContext())
            {
                var processor =
                    new SqsMessageProcessor(dbContext);

                await processor.ProcessAsync(
                    message,
                    CancellationToken.None);
            }

            await using var verificationDbContext =
                CreateDbContext();

            var count =
                await verificationDbContext
                    .Set<ProcessedMessage>()
                    .CountAsync(x =>
                        x.MessageId ==
                        applicationMessageId);

            Assert.Equal(1, count);
        }
        finally
        {
            await using var cleanupDbContext =
                CreateDbContext();

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
        var applicationMessageId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        var integrationEvent =
            new TransferCompleted(
                transferId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                100m,
                DateTimeOffset.UtcNow);

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
            await using (var dbContext = CreateDbContext())
            {
                var processor =
                    new SqsMessageProcessor(dbContext);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    processor.ProcessAsync(
                        message,
                        CancellationToken.None));
            }

            await using var verificationDbContext =
                CreateDbContext();

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
                CreateDbContext();

            await cleanupDbContext
                .Set<ProcessedMessage>()
                .Where(x =>
                    x.MessageId ==
                    applicationMessageId)
                .ExecuteDeleteAsync();
        }
    }
}
