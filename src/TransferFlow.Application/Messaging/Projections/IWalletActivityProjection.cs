namespace TransferFlow.Application.Messaging.Projections;

public interface IWalletActivityProjection
{
    Task UpsertAsync(
        WalletActivity activity,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WalletActivity>> GetRecentAsync(
        Guid walletId,
        int limit,
        CancellationToken cancellationToken);
}
