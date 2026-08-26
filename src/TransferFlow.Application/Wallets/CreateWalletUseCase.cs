using TransferFlow.Domain;

namespace TransferFlow.Application.Wallets;

public sealed class CreateWalletUseCase
{
    private readonly IWalletRepository _walletRepository;

    public CreateWalletUseCase(
        IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletResponse> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var wallet = new Wallet();
        _walletRepository.Add(wallet);
        await _walletRepository.SaveChangesAsync(cancellationToken);
        return new WalletResponse(wallet.Id, wallet.Balance);
    }
}