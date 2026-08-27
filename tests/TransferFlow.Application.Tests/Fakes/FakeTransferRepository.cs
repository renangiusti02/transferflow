using TransferFlow.Application.Transfers;
using TransferFlow.Domain;

namespace TransferFlow.Application.Tests.Fakes;

public sealed class FakeTransferRepository : ITransferRepository
{
    public List<Transfer> Transfers { get; } = [];

    public void Add(Transfer transfer)
    {
        Transfers.Add(transfer);
    }

    public Task<Transfer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var transfer = Transfers.FirstOrDefault(
            transfer => transfer.Id == id);

        return Task.FromResult(transfer);
    }
}