namespace TransferFlow.Application.Wallets;

public sealed class GetWalletByIdUseCase
{
    private readonly IWalletRepository _walletRepository;

    public GetWalletByIdUseCase(
        IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletResponse?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var wallet = await _walletRepository.GetByIdAsync(id, cancellationToken);
        return wallet is null ? null : new WalletResponse(wallet.Id, wallet.Balance);
    }
}