using TransferFlow.Domain;

namespace TransferFlow.Application.Wallets;

public interface IWalletRepository
{
    void Add(
        Wallet wallet);

    Task<Wallet?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Wallet?> GetForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}