using TransferFlow.Application.Common;
using TransferFlow.Domain;

namespace TransferFlow.Application.Wallets;

public sealed class CreateWalletUseCase
{
    private readonly IWalletRepository _walletRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateWalletUseCase(
        IWalletRepository walletRepository,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<WalletResponse> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var wallet = new Wallet();
        _walletRepository.Add(wallet);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new WalletResponse(wallet.Id, wallet.Balance);
    }
}