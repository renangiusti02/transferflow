using TransferFlow.Application.Common;
using TransferFlow.Application.Messaging;
using TransferFlow.Application.Messaging.Events;
using TransferFlow.Application.Wallets;
using TransferFlow.Domain;

namespace TransferFlow.Application.Transfers;

public sealed class CreateTransferUseCase
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutbox _outbox;
    private const int MaxConcurrencyAttempts = 3;

    public CreateTransferUseCase(
        IWalletRepository walletRepository,
        ITransferRepository transferRepository,
        IUnitOfWork unitOfWork,
        IOutbox outbox)
    {
        _walletRepository = walletRepository;
        _transferRepository = transferRepository;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
    }

    public async Task<TransferResponse> ExecuteAsync(
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1;
             attempt < MaxConcurrencyAttempts;
             attempt++)
        {
            try
            {
                return await ExecuteAttemptAsync(
                    sourceWalletId,
                    destinationWalletId,
                    amount,
                    idempotencyKey,
                    cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // Retry the entire operation using fresh database state.
            }
        }

        return await ExecuteAttemptAsync(
            sourceWalletId,
            destinationWalletId,
            amount,
            idempotencyKey,
            cancellationToken);
    }

    private async Task<TransferResponse> ExecuteAttemptAsync(
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var transfer = new Transfer(
            sourceWalletId,
            destinationWalletId,
            amount,
            idempotencyKey);

        var existingTransfer =
            await _transferRepository.GetByIdempotencyKeyAsync(
                idempotencyKey,
                cancellationToken);

        if (existingTransfer is not null)
        {
            return ResolveExistingTransfer(
                existingTransfer,
                sourceWalletId,
                destinationWalletId,
                amount);
        }

        var sourceWallet =
            await _walletRepository.GetForUpdateAsync(
                sourceWalletId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Source wallet not found");

        var destinationWallet =
            await _walletRepository.GetForUpdateAsync(
                destinationWalletId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Destination wallet not found");

        sourceWallet.Debit(amount);
        destinationWallet.Credit(amount);

        _transferRepository.Add(transfer);

        _outbox.Add(
            new TransferCompleted(
                transfer.Id,
                transfer.SourceWalletId,
                transfer.DestinationWalletId,
                transfer.Amount,
                transfer.CreatedAtUtc));

        try
        {
            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return ToResponse(transfer);
        }
        catch (DuplicateIdempotencyKeyException)
        {
            existingTransfer =
                await _transferRepository
                    .GetByIdempotencyKeyAsync(
                        idempotencyKey,
                        cancellationToken);

            if (existingTransfer is null)
            {
                throw;
            }

            return ResolveExistingTransfer(
                existingTransfer,
                sourceWalletId,
                destinationWalletId,
                amount);
        }
    }

    private static TransferResponse ResolveExistingTransfer(
        Transfer existingTransfer,
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount)
    {
        if (existingTransfer.SourceWalletId != sourceWalletId ||
            existingTransfer.DestinationWalletId != destinationWalletId ||
            existingTransfer.Amount != amount)
        {
            throw new InvalidOperationException(
                "Idempotency key was already used with different transfer data.");
        }

        return ToResponse(existingTransfer);
    }

    private static TransferResponse ToResponse(
        Transfer transfer)
    {
        return new TransferResponse(
            transfer.Id,
            transfer.SourceWalletId,
            transfer.DestinationWalletId,
            transfer.Amount,
            transfer.CreatedAtUtc);
    }
}
