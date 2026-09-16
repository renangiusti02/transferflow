using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System.Text.Json;
using TransferFlow.Application.Messaging;
using TransferFlow.Application.Messaging.Events;
using TransferFlow.Application.Transfers;
using TransferFlow.Domain;
using TransferFlow.Infrastructure.Messaging.Outbox;
using TransferFlow.Infrastructure.Persistence;
using TransferFlow.Infrastructure.Persistence.Repositories;
using TransferFlow.Integration.Tests.Infrastructure;
using Xunit;

namespace TransferFlow.Integration.Tests.Outbox;

[Collection("PostgreSQL integration tests")]
public sealed class OutboxProcessorTests
{
    private readonly string _connectionString;

    public OutboxProcessorTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<OutboxProcessorTests>()
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

    private async Task CleanupAsync(
        Guid outboxMessageId)
    {
        await using var dbContext =
            CreateDbContext();

        var message =
            await dbContext
                .Set<OutboxMessage>()
                .SingleOrDefaultAsync(
                    message =>
                        message.Id == outboxMessageId);

        if (message is not null)
        {
            dbContext
                .Set<OutboxMessage>()
                .Remove(message);

            await dbContext.SaveChangesAsync();
        }
    }

    private sealed class FakeOutboxPublisher(bool shouldFail = false) : IOutboxPublisher
    {
        private readonly bool _shouldFail = shouldFail;

        public List<Guid> PublishedMessageIds { get; } = [];

        public Task PublishAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            if (_shouldFail)
            {
                throw new InvalidOperationException(
                    "Simulated publish failure.");
            }

            PublishedMessageIds.Add(message.Id);

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Outbox_Processor_Process_Pending_Async_Should_Mark_Message_As_Processed_When_Publish_Succeeds()
    {
        await using var dbContext = CreateDbContext();

        var publisher = new FakeOutboxPublisher();
        var outboxProcessor = new OutboxProcessor(dbContext, publisher);

        var transferId = Guid.NewGuid();
        var transferCompletedEvent = new TransferCompleted(
            transferId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            DateTimeOffset.UtcNow);

        var outboxMessage = new OutboxMessage(
            "TransferCompleted",
            JsonSerializer.Serialize(transferCompletedEvent),
            transferId,
            DateTimeOffset.UtcNow);

        try
        {
            dbContext.Set<OutboxMessage>().Add(outboxMessage);

            await dbContext.SaveChangesAsync(CancellationToken.None);

            await outboxProcessor.ProcessPendingAsync(CancellationToken.None);

            await using var verificationDbContext = CreateDbContext();

            var persistedMessage =
                await verificationDbContext
                    .Set<OutboxMessage>()
                    .AsNoTracking()
                    .SingleAsync(
                        message => message.Id == outboxMessage.Id);

            Assert.NotNull(persistedMessage);
            Assert.NotNull(persistedMessage.ProcessedAtUtc);
            Assert.Equal(outboxMessage.Id, persistedMessage.Id);
            Assert.Contains(
                outboxMessage.Id,
                publisher.PublishedMessageIds);
        }
        finally
        {
            await CleanupAsync(outboxMessage.Id);
        }
    }

    [Fact]
    public async Task Outbox_Processor_Process_Pending_Async_Should_Not_Mark_Message_As_Processed_When_Publish_Fails()
    {
        await using var dbContext = CreateDbContext();

        var publisher = new FakeOutboxPublisher(shouldFail: true);
        var outboxProcessor = new OutboxProcessor(dbContext, publisher);

        var transferId = Guid.NewGuid();
        var transferCompletedEvent = new TransferCompleted(
            transferId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            DateTimeOffset.UtcNow);

        var outboxMessage = new OutboxMessage(
            "TransferCompleted",
            JsonSerializer.Serialize(transferCompletedEvent),
            transferId,
            DateTimeOffset.UtcNow);

        try
        {
            dbContext.Set<OutboxMessage>().Add(outboxMessage);

            await dbContext.SaveChangesAsync(CancellationToken.None);

            await outboxProcessor.ProcessPendingAsync(CancellationToken.None);

            await using var verificationDbContext = CreateDbContext();

            var persistedMessage =
                await verificationDbContext
                    .Set<OutboxMessage>()
                    .AsNoTracking()
                    .SingleAsync(
                        message => message.Id == outboxMessage.Id);

            Assert.NotNull(persistedMessage);
            Assert.Null(persistedMessage.ProcessedAtUtc);
            Assert.Empty(publisher.PublishedMessageIds);
        }
        finally
        {
            await CleanupAsync(outboxMessage.Id);
        }
    }
}
