namespace TransferFlow.Application.Messaging.Projections;

public sealed record WalletActivity(
    Guid TransferId,
    Guid WalletId,
    Guid CounterpartyWalletId,
    decimal Amount,
    WalletActivityDirection Direction,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId);
