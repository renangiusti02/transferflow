using TransferFlow.Application.Messaging.Projections;

namespace TransferFlow.Application.Wallets;

public sealed record WalletActivityResponse(
    Guid TransferId,
    Guid CounterpartyWalletId,
    decimal Amount,
    WalletActivityDirection Direction,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId);
