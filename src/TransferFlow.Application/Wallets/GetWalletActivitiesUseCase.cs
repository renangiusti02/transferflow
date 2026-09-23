using TransferFlow.Application.Messaging.Projections;

namespace TransferFlow.Application.Wallets;

public sealed class GetWalletActivitiesUseCase(
    IWalletRepository walletRepository,
    IWalletActivityProjection projection)
{
    private readonly IWalletRepository _walletRepository = walletRepository;
    private readonly IWalletActivityProjection _projection = projection;

    public async Task<IReadOnlyList<WalletActivityResponse>?>
        ExecuteAsync(
            Guid walletId,
            int limit,
            CancellationToken cancellationToken = default)
    {
        var wallet =
            await _walletRepository.GetByIdAsync(
                walletId,
                cancellationToken);

        if (wallet is null)
        {
            return null;
        }

        var activities =
            await _projection.GetRecentAsync(
                walletId,
                limit,
                cancellationToken);

        return [
            ..
            activities
                .Select(activity =>
                    new WalletActivityResponse(
                        activity.TransferId,
                        activity.CounterpartyWalletId,
                        activity.Amount,
                        activity.Direction,
                        activity.OccurredAtUtc,
                        activity.CorrelationId))
        ];
    }
}
