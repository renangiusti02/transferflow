using Microsoft.EntityFrameworkCore;
using TransferFlow.Application.Wallets;
using TransferFlow.Infrastructure.Persistence;
using TransferFlow.Infrastructure.Persistence.Repositories;
using TransferFlow.Integration.Tests.Infrastructure;

namespace TransferFlow.Integration.Tests.Wallets;

[Collection("PostgreSQL integration tests")]
public sealed class CreateWalletTests(
    PostgreSqlIntegrationTestFixture database)
{
    private readonly PostgreSqlIntegrationTestFixture _database = database;

    [Fact]
    public async Task Create_Wallet_Should_Persist_Wallet()
    {
        Guid? walletId = null;

        try
        {
            await using var dbContext =
                _database.CreateDbContext();

            var repository =
                new WalletRepository(dbContext);

            var unitOfWork =
                new EfUnitOfWork(dbContext);

            var useCase =
                new CreateWalletUseCase(
                    repository,
                    unitOfWork);

            var response =
                await useCase.ExecuteAsync();

            walletId = response.Id;

            await using var verificationDbContext =
                _database.CreateDbContext();

            var wallet =
                await verificationDbContext.Wallets
                    .AsNoTracking()
                    .SingleAsync(
                        wallet =>
                            wallet.Id == response.Id);

            Assert.Equal(response.Id, wallet.Id);
            Assert.Equal(0m, wallet.Balance);
            Assert.Equal(0m, response.Balance);
        }
        finally
        {
            if (walletId is not null)
            {
                await using var cleanupDbContext =
                    _database.CreateDbContext();

                var wallet =
                    await cleanupDbContext.Wallets
                        .SingleOrDefaultAsync(
                            wallet =>
                                wallet.Id == walletId.Value);

                if (wallet is not null)
                {
                    cleanupDbContext.Wallets.Remove(wallet);
                    await cleanupDbContext.SaveChangesAsync();
                }
            }
        }
    }
}
