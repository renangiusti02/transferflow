using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using TransferFlow.Application.Messaging;
using TransferFlow.Application.Messaging.Events;
using TransferFlow.Application.Observability;
using TransferFlow.Application.Transfers;
using TransferFlow.Domain;
using TransferFlow.Infrastructure.Messaging.Outbox;
using TransferFlow.Infrastructure.Persistence;
using TransferFlow.Infrastructure.Persistence.Repositories;

namespace TransferFlow.Integration.Tests.Transfers;

[Collection("PostgreSQL integration tests")]
public sealed class CreateTransferOutboxTests
{
    private readonly string _connectionString;

    public CreateTransferOutboxTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CreateTransferOutboxTests>()
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

    private async Task<(Guid SourceWalletId, Guid DestinationWalletId)>
        CreateWalletsAsync(
            CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();

        var sourceWallet = new Wallet();
        sourceWallet.Credit(100m);

        var destinationWallet = new Wallet();

        dbContext.Wallets.Add(sourceWallet);
        dbContext.Wallets.Add(destinationWallet);

        await dbContext.SaveChangesAsync(cancellationToken);

        return (
            sourceWallet.Id,
            destinationWallet.Id);
    }

    private async Task<TransferResponse> ExecuteTransferAsync(
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();

        var walletRepository =
            new WalletRepository(dbContext);

        var transferRepository =
            new TransferRepository(dbContext);

        var unitOfWork =
            new EfUnitOfWork(dbContext);

        var outbox =
            new EfOutbox(dbContext);

        var useCase =
            new CreateTransferUseCase(
                walletRepository,
                transferRepository,
                unitOfWork,
                outbox,
                new StubCorrelationContext(Guid.NewGuid()),
                NullLogger<CreateTransferUseCase>.Instance);

        return await useCase.ExecuteAsync(
            sourceWalletId,
            destinationWalletId,
            amount,
            idempotencyKey,
            cancellationToken);
    }

    private async Task CleanupOutboxMessagesAsync(
        TransferFlowDbContext dbContext,
        IEnumerable<Guid> transferIds)
    {
        var ids = transferIds.ToHashSet();

        var messages = await dbContext
            .Set<OutboxMessage>()
            .Where(message =>
                message.Type == nameof(TransferCompleted))
            .ToListAsync();

        var messagesToRemove = messages
            .Where(message =>
            {
                var integrationEvent =
                    JsonSerializer.Deserialize<TransferCompleted>(
                        message.Payload);

                return integrationEvent is not null &&
                    ids.Contains(integrationEvent.TransferId);
            })
            .ToList();

        dbContext.Set<OutboxMessage>()
            .RemoveRange(messagesToRemove);
    }

    private async Task CleanupAsync(
        Guid sourceWalletId,
        Guid destinationWalletId,
        string idempotencyKey)
    {
        await using var dbContext = CreateDbContext();

        var transfers = await dbContext.Transfers
            .Where(transfer =>
                transfer.IdempotencyKey == idempotencyKey)
            .ToListAsync();

        await CleanupOutboxMessagesAsync(
            dbContext,
            transfers.Select(transfer => transfer.Id));

        dbContext.Transfers.RemoveRange(transfers);

        var wallets = await dbContext.Wallets
            .Where(wallet =>
                wallet.Id == sourceWalletId ||
                wallet.Id == destinationWalletId)
            .ToListAsync();

        dbContext.Wallets.RemoveRange(wallets);

        await dbContext.SaveChangesAsync();
    }

    internal sealed class StubCorrelationContext(
        Guid correlationId)
        : ICorrelationContext
    {
        public Guid CorrelationId { get; } =
            correlationId;
    }

    [Fact]
    public async Task Create_Transfer_Should_Persist_Pending_Outbox_Message()
    {
        var (sourceWalletId, destinationWalletId) =
            await CreateWalletsAsync();

        const decimal amount = 30m;

        var idempotencyKey =
            $"transfer-{Guid.NewGuid():N}";

        try
        {
            using var cancellationTokenSource =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(15));

            var response = await ExecuteTransferAsync(
                sourceWalletId,
                destinationWalletId,
                amount,
                idempotencyKey,
                cancellationTokenSource.Token);

            await using var verificationDbContext =
                CreateDbContext();

            var sourceWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet => wallet.Id == sourceWalletId);

            var destinationWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet => wallet.Id == destinationWalletId);

            var transfer =
                await verificationDbContext.Transfers
                    .AsNoTracking()
                    .SingleAsync(
                        transfer => transfer.Id == response.Id);

            var outboxMessages =
                await verificationDbContext
                    .Set<OutboxMessage>()
                    .AsNoTracking()
                    .Where(message =>
                        message.Type == nameof(TransferCompleted))
                    .ToListAsync();

            var transferOutboxMessage = outboxMessages.Where(message =>
            {
                var integrationEvent =
                    JsonSerializer.Deserialize<TransferCompleted>(
                        message.Payload);

                return integrationEvent?.TransferId ==
                    transfer.Id;
            });

            var outboxMessage =
                Assert.Single(transferOutboxMessage);

            var transferCompleted =
                JsonSerializer.Deserialize<TransferCompleted>(
                    outboxMessage.Payload);

            Assert.NotNull(transferCompleted);

            Assert.Equal(
                transfer.Id,
                transferCompleted.TransferId);

            Assert.Equal(
                transfer.SourceWalletId,
                transferCompleted.SourceWalletId);

            Assert.Equal(
                transfer.DestinationWalletId,
                transferCompleted.DestinationWalletId);

            Assert.Equal(
                transfer.Amount,
                transferCompleted.Amount);

            Assert.True(
                (transfer.CreatedAtUtc -
                    transferCompleted.OccurredAtUtc)
                .Duration() <= TimeSpan.FromMicroseconds(1));

            Assert.Equal(
                transfer.CreatedAtUtc,
                outboxMessage.OccurredAtUtc);

            Assert.Null(
                outboxMessage.ProcessedAtUtc);

            Assert.NotEqual(
                transfer.Id,
                outboxMessage.CorrelationId);
        }
        finally
        {
            await CleanupAsync(
                sourceWalletId,
                destinationWalletId,
                idempotencyKey);
        }
    }

    private sealed class InvalidPayloadOutbox(TransferFlowDbContext dbContext) : IOutbox
    {
        private readonly TransferFlowDbContext _dbContext = dbContext;

        public Guid? MessageId { get; private set; }

        public void Add<T>(T message)
            where T : IIntegrationEvent
        {
            var outboxMessage = new OutboxMessage(
                typeof(T).Name,
                "{ invalid json",
                Guid.NewGuid(),
                message.OccurredAtUtc);

            MessageId = outboxMessage.Id;

            _dbContext.Set<OutboxMessage>()
                .Add(outboxMessage);
        }
    }

    [Fact]
    public async Task Create_Transfer_Should_Rollback_Transfer_And_Outbox_When_Commit_Fails()
    {
        var (sourceWalletId, destinationWalletId) =
            await CreateWalletsAsync();

        const decimal amount = 30m;

        var idempotencyKey =
            $"transfer-{Guid.NewGuid():N}";

        try
        {
            await using var dbContext = CreateDbContext();

            var walletRepository =
                new WalletRepository(dbContext);

            var transferRepository =
                new TransferRepository(dbContext);

            var unitOfWork =
                new EfUnitOfWork(dbContext);

            var invalidOutbox =
                new InvalidPayloadOutbox(dbContext);

            var useCase =
                new CreateTransferUseCase(
                    walletRepository,
                    transferRepository,
                    unitOfWork,
                    invalidOutbox,
                    new StubCorrelationContext(Guid.NewGuid()),
                    NullLogger<CreateTransferUseCase>.Instance);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                useCase.ExecuteAsync(
                    sourceWalletId,
                    destinationWalletId,
                    amount,
                    idempotencyKey));

            await using var verificationDbContext =
                CreateDbContext();

            var sourceWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet => wallet.Id == sourceWalletId);

            var destinationWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet => wallet.Id == destinationWalletId);

            Assert.Equal(
                100m,
                sourceWallet.Balance);

            Assert.Equal(
                0m,
                destinationWallet.Balance);

            var transferExists =
                await verificationDbContext.Transfers
                    .AsNoTracking()
                    .AnyAsync(
                        transfer =>
                            transfer.IdempotencyKey ==
                            idempotencyKey);

            Assert.False(transferExists);

            Assert.NotNull(
                invalidOutbox.MessageId);

            var outboxMessageExists =
                await verificationDbContext
                    .Set<OutboxMessage>()
                    .AsNoTracking()
                    .AnyAsync(
                        message =>
                            message.Id ==
                            invalidOutbox.MessageId.Value);

            Assert.False(outboxMessageExists);
        }
        finally
        {
            await CleanupAsync(
                sourceWalletId,
                destinationWalletId,
                idempotencyKey);
        }
    }
}
