using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TransferFlow.Application.Messaging.Events;
using TransferFlow.Application.Transfers;
using TransferFlow.Domain;
using TransferFlow.Infrastructure.Messaging.Outbox;
using TransferFlow.Infrastructure.Persistence;
using TransferFlow.Infrastructure.Persistence.Repositories;
using TransferFlow.Integration.Tests.Infrastructure;
using Xunit;

namespace TransferFlow.Integration.Tests.Transfers;

[Collection("PostgreSQL integration tests")]
public sealed class CreateTransferConcurrencyTests
{
    private readonly string _connectionString;

    public CreateTransferConcurrencyTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CreateTransferConcurrencyTests>()
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
        AsyncBarrier barrier,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();

        var walletRepository =
            new WalletRepository(dbContext);

        var transferRepository =
            new TransferRepository(dbContext);

        var unitOfWork =
            new EfUnitOfWork(dbContext);

        var coordinatedUnitOfWork =
            new CoordinatedUnitOfWork(
                unitOfWork,
                barrier);

        var outbox =
            new EfOutbox(dbContext);

        var useCase =
            new CreateTransferUseCase(
                walletRepository,
                transferRepository,
                coordinatedUnitOfWork,
                outbox);

        return await useCase.ExecuteAsync(
            sourceWalletId,
            destinationWalletId,
            amount,
            idempotencyKey,
            cancellationToken);
    }

    private async Task<(
        Guid SourceWalletId,
        Guid FirstDestinationWalletId,
        Guid SecondDestinationWalletId)>
        CreateThreeWalletsAsync(
            CancellationToken cancellationToken = default)
    {
        await using var dbContext = CreateDbContext();

        var sourceWallet = new Wallet();
        sourceWallet.Credit(100m);

        var firstDestinationWallet = new Wallet();
        var secondDestinationWallet = new Wallet();

        dbContext.Wallets.AddRange(
            sourceWallet,
            firstDestinationWallet,
            secondDestinationWallet);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        return (
            sourceWallet.Id,
            firstDestinationWallet.Id,
            secondDestinationWallet.Id);
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

    private async Task CleanupAsync(
        Guid sourceWalletId,
        Guid firstDestinationWalletId,
        Guid secondDestinationWalletId,
        string firstIdempotencyKey,
        string secondIdempotencyKey)
    {
        await using var dbContext = CreateDbContext();

        var transfers = await dbContext.Transfers
            .Where(transfer =>
                transfer.IdempotencyKey == firstIdempotencyKey ||
                transfer.IdempotencyKey == secondIdempotencyKey)
            .ToListAsync();

        await CleanupOutboxMessagesAsync(
            dbContext,
            transfers.Select(transfer => transfer.Id));

        dbContext.Transfers.RemoveRange(transfers);

        var wallets = await dbContext.Wallets
            .Where(wallet =>
                wallet.Id == sourceWalletId ||
                wallet.Id == firstDestinationWalletId ||
                wallet.Id == secondDestinationWalletId)
            .ToListAsync();

        dbContext.Wallets.RemoveRange(wallets);

        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_Transfer_With_Concurrent_Requests_And_Same_Idempotency_Key_Should_Create_One_Transfer()
    {
        var (sourceWalletId, destinationWalletId) =
            await CreateWalletsAsync();

        const decimal amount = 30m;

        var idempotencyKey =
            $"concurrent-{Guid.NewGuid():N}";

        try
        {
            var barrier = new AsyncBarrier(2);

            using var cancellationTokenSource =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(15));

            var firstTask = ExecuteTransferAsync(
                sourceWalletId,
                destinationWalletId,
                amount,
                idempotencyKey,
                barrier,
                cancellationTokenSource.Token);

            var secondTask = ExecuteTransferAsync(
                sourceWalletId,
                destinationWalletId,
                amount,
                idempotencyKey,
                barrier,
                cancellationTokenSource.Token);

            var responses = await Task.WhenAll(
                firstTask,
                secondTask);

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

            var transfers =
                await verificationDbContext.Transfers
                    .AsNoTracking()
                    .Where(
                        transfer =>
                            transfer.IdempotencyKey ==
                            idempotencyKey)
                    .ToListAsync();

            Assert.Equal(
                responses[0].Id,
                responses[1].Id);

            Assert.Single(transfers);

            Assert.Equal(
                responses[0].Id,
                transfers[0].Id);

            Assert.Equal(
                70m,
                sourceWallet.Balance);

            Assert.Equal(
                30m,
                destinationWallet.Balance);
        }
        finally
        {
            await CleanupAsync(
                sourceWalletId,
                destinationWalletId,
                idempotencyKey);
        }
    }

    [Fact]
    public async Task Create_Transfer_With_Concurrent_Requests_And_Same_Idempotency_Key_And_Different_Payloads_Should_Accept_Only_One()
    {
        var (sourceWalletId, destinationWalletId) =
            await CreateWalletsAsync();

        const decimal firstAmount = 30m;
        const decimal secondAmount = 40m;

        var idempotencyKey =
            $"concurrent-conflict-{Guid.NewGuid():N}";

        try
        {
            var barrier = new AsyncBarrier(2);

            using var cancellationTokenSource =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(15));

            var firstTask = ExecuteTransferAsync(
                sourceWalletId,
                destinationWalletId,
                firstAmount,
                idempotencyKey,
                barrier,
                cancellationTokenSource.Token);

            var secondTask = ExecuteTransferAsync(
                sourceWalletId,
                destinationWalletId,
                secondAmount,
                idempotencyKey,
                barrier,
                cancellationTokenSource.Token);

            TransferResponse? firstResponse = null;
            TransferResponse? secondResponse = null;

            Exception? firstException = null;
            Exception? secondException = null;

            try
            {
                firstResponse = await firstTask;
            }
            catch (Exception exception)
            {
                firstException = exception;
            }

            try
            {
                secondResponse = await secondTask;
            }
            catch (Exception exception)
            {
                secondException = exception;
            }

            var responses = new[]
            {
                firstResponse,
                secondResponse
            }
            .Where(response => response is not null)
            .ToList();

            var exceptions = new[]
            {
                firstException,
                secondException
            }
            .Where(exception => exception is not null)
            .ToList();

            var successfulResponse =
                Assert.Single(responses);

            Assert.NotNull(successfulResponse);

            var failedException =
                Assert.Single(exceptions);

            Assert.IsType<InvalidOperationException>(
                failedException);

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

            var transfers =
                await verificationDbContext.Transfers
                    .AsNoTracking()
                    .Where(
                        transfer =>
                            transfer.IdempotencyKey ==
                            idempotencyKey)
                    .ToListAsync();

            var persistedTransfer =
                Assert.Single(transfers);

            Assert.Equal(
                successfulResponse.Id,
                persistedTransfer.Id);

            Assert.Equal(
                successfulResponse.Amount,
                persistedTransfer.Amount);

            Assert.Equal(
                100m - persistedTransfer.Amount,
                sourceWallet.Balance);

            Assert.Equal(
                persistedTransfer.Amount,
                destinationWallet.Balance);

            Assert.True(
                persistedTransfer.Amount == firstAmount ||
                persistedTransfer.Amount == secondAmount);
        }
        finally
        {
            await CleanupAsync(
                sourceWalletId,
                destinationWalletId,
                idempotencyKey);
        }
    }

    [Fact]
    public async Task Create_Transfer_With_Concurrent_Requests_And_Different_Keys_Should_Not_Overspend_Source_Wallet()
    {
        var (
            sourceWalletId,
            firstDestinationWalletId,
            secondDestinationWalletId) =
            await CreateThreeWalletsAsync();

        const decimal amount = 80m;

        var firstIdempotencyKey =
            $"overspending-a-{Guid.NewGuid():N}";

        var secondIdempotencyKey =
            $"overspending-b-{Guid.NewGuid():N}";

        try
        {
            var barrier =
                new AsyncBarrier(2);

            using var cancellationTokenSource =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(15));

            var firstTask = ExecuteTransferAsync(
                sourceWalletId,
                firstDestinationWalletId,
                amount,
                firstIdempotencyKey,
                barrier,
                cancellationTokenSource.Token);

            var secondTask = ExecuteTransferAsync(
                sourceWalletId,
                secondDestinationWalletId,
                amount,
                secondIdempotencyKey,
                barrier,
                cancellationTokenSource.Token);

            TransferResponse? firstResponse = null;
            TransferResponse? secondResponse = null;

            Exception? firstException = null;
            Exception? secondException = null;

            try
            {
                firstResponse = await firstTask;
            }
            catch (Exception exception)
            {
                firstException = exception;
            }

            try
            {
                secondResponse = await secondTask;
            }
            catch (Exception exception)
            {
                secondException = exception;
            }

            var responses = new[]
            {
            firstResponse,
            secondResponse
        }
            .Where(response => response is not null)
            .ToList();

            var exceptions = new[]
            {
            firstException,
            secondException
        }
            .Where(exception => exception is not null)
            .ToList();

            var successfulResponse =
                Assert.Single(responses);

            var failedException =
                Assert.Single(exceptions);

            var invalidOperationException =
                Assert.IsType<InvalidOperationException>(
                    failedException);

            Assert.Equal(
                "Insufficient funds.",
                invalidOperationException.Message);

            await using var verificationDbContext =
                CreateDbContext();

            var sourceWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet =>
                            wallet.Id == sourceWalletId);

            var firstDestinationWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet =>
                            wallet.Id == firstDestinationWalletId);

            var secondDestinationWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet =>
                            wallet.Id == secondDestinationWalletId);

            var totalTransferred =
                firstDestinationWallet.Balance +
                secondDestinationWallet.Balance;

            var transfers = await verificationDbContext.Transfers
                .AsNoTracking()
                .Where(
                    transfer =>
                        transfer.IdempotencyKey ==
                        firstIdempotencyKey ||
                        transfer.IdempotencyKey ==
                        secondIdempotencyKey)
                .ToListAsync();

            var persistedTransfer =
                Assert.Single(transfers);

            Assert.NotNull(successfulResponse);

            Assert.Equal(
                successfulResponse.Id,
                persistedTransfer.Id);

            Assert.Equal(
                amount,
                persistedTransfer.Amount);

            Assert.True(
                firstDestinationWallet.Balance == amount &&
                secondDestinationWallet.Balance == 0m
                ||
                firstDestinationWallet.Balance == 0m &&
                secondDestinationWallet.Balance == amount);

            Assert.Equal(
                80m,
                totalTransferred);

            Assert.Equal(
                20m,
                sourceWallet.Balance);

            Assert.Equal(
                100m,
                sourceWallet.Balance +
                totalTransferred);
        }
        finally
        {
            await CleanupAsync(
                sourceWalletId,
                firstDestinationWalletId,
                secondDestinationWalletId,
                firstIdempotencyKey,
                secondIdempotencyKey);
        }
    }

    [Fact]
    public async Task Create_Transfer_With_Concurrent_Requests_And_Different_Keys_Should_Both_Succeed_When_Balance_Is_Sufficient()
    {
        var (
            sourceWalletId,
            firstDestinationWalletId,
            secondDestinationWalletId) =
            await CreateThreeWalletsAsync();

        const decimal amount = 30m;

        var firstIdempotencyKey =
            $"concurrent-valid-a-{Guid.NewGuid():N}";

        var secondIdempotencyKey =
            $"concurrent-valid-b-{Guid.NewGuid():N}";

        try
        {
            var barrier =
            new AsyncBarrier(2);

            using var cancellationTokenSource =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(15));

            var firstTask = ExecuteTransferAsync(
                sourceWalletId,
                firstDestinationWalletId,
                amount,
                firstIdempotencyKey,
                barrier,
                cancellationTokenSource.Token);

            var secondTask = ExecuteTransferAsync(
                sourceWalletId,
                secondDestinationWalletId,
                amount,
                secondIdempotencyKey,
                barrier,
                cancellationTokenSource.Token);

            var responses = await Task.WhenAll(
                firstTask,
                secondTask);

            await using var verificationDbContext =
                CreateDbContext();

            var sourceWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet =>
                            wallet.Id == sourceWalletId);

            var firstDestinationWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet =>
                            wallet.Id == firstDestinationWalletId);

            var secondDestinationWallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet =>
                            wallet.Id == secondDestinationWalletId);

            var transfers =
                await verificationDbContext.Transfers
                    .AsNoTracking()
                    .Where(transfer =>
                        transfer.IdempotencyKey == firstIdempotencyKey ||
                        transfer.IdempotencyKey == secondIdempotencyKey)
                    .ToListAsync();

            Assert.Equal(2, responses.Length);
            Assert.Equal(2, transfers.Count);

            Assert.Equal(
                40m,
                sourceWallet.Balance);

            Assert.Equal(
                30m,
                firstDestinationWallet.Balance);

            Assert.Equal(
                30m,
                secondDestinationWallet.Balance);

            Assert.Equal(
                100m,
                sourceWallet.Balance +
                firstDestinationWallet.Balance +
                secondDestinationWallet.Balance);
        }
        finally
        {
            await CleanupAsync(
                sourceWalletId,
                firstDestinationWalletId,
                secondDestinationWalletId,
                firstIdempotencyKey,
                secondIdempotencyKey);
        }
    }
}
