using TransferFlow.Application.Common;
using TransferFlow.Application.Wallets;
using TransferFlow.Domain;

namespace TransferFlow.Application.Transfers;

public sealed class CreateTransferUseCase
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateTransferUseCase(
        IWalletRepository walletRepository,
        ITransferRepository transferRepository,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _transferRepository = transferRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TransferResponse> ExecuteAsync(
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var transfer = new Transfer(sourceWalletId, destinationWalletId, amount);

        var sourceWallet = await _walletRepository.GetForUpdateAsync(sourceWalletId, cancellationToken) ??
            throw new KeyNotFoundException("Source wallet not found");

        var destinationWallet = await _walletRepository.GetForUpdateAsync(destinationWalletId, cancellationToken) ?? 
            throw new KeyNotFoundException("Destination wallet not found");

        sourceWallet.Debit(amount);
        destinationWallet.Credit(amount);

        _transferRepository.Add(transfer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new TransferResponse(transfer.Id, transfer.SourceWalletId, transfer.DestinationWalletId, transfer.Amount, transfer.CreatedAtUtc);
    }
}