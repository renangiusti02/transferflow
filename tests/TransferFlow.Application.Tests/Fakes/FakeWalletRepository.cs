using TransferFlow.Application.Wallets;
using TransferFlow.Domain;

namespace TransferFlow.Application.Tests.Fakes;

public sealed class FakeWalletRepository : IWalletRepository
{
    private readonly Dictionary<Guid, Wallet> _wallets = [];

    public void Add(Wallet wallet)
    {
        _wallets[wallet.Id] = wallet;
    }

    public Task<Wallet?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _wallets.TryGetValue(id, out var wallet);

        return Task.FromResult(wallet);
    }

    public Task<Wallet?> GetForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _wallets.TryGetValue(id, out var wallet);

        return Task.FromResult(wallet);
    }
}