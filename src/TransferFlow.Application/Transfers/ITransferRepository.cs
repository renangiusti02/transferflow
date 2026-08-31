using TransferFlow.Domain;

namespace TransferFlow.Application.Transfers;

public interface ITransferRepository
{
    void Add(Transfer transfer);

    Task<Transfer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Transfer?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}