using Microsoft.EntityFrameworkCore;
using TransferFlow.Application.Wallets;
using TransferFlow.Domain;

namespace TransferFlow.Infrastructure.Persistence.Repositories;

public sealed class WalletRepository : IWalletRepository
{
    private readonly TransferFlowDbContext _dbContext;

    public WalletRepository(
        TransferFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(
        Wallet wallet)
    {
        _dbContext.Wallets.Add(wallet);
    }

    public async Task<Wallet?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Wallet?> GetForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Wallets
            .FirstOrDefaultAsync(
                wallet => wallet.Id == id,
                cancellationToken);
    }
}