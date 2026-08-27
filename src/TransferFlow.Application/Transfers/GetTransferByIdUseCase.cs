namespace TransferFlow.Application.Transfers;

public sealed class GetTransferByIdUseCase
{
    private readonly ITransferRepository _transferRepository;

    public GetTransferByIdUseCase(
        ITransferRepository transferRepository)
    {
        _transferRepository = transferRepository;
    }

    public async Task<TransferResponse?> ExecuteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetByIdAsync(
            id,
            cancellationToken);

        return transfer is null
            ? null
            : new TransferResponse(
                transfer.Id,
                transfer.SourceWalletId,
                transfer.DestinationWalletId,
                transfer.Amount,
                transfer.CreatedAtUtc);
    }
}