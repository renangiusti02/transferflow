namespace TransferFlow.Application.Messaging.Events;

public sealed record TransferCompleted(
    Guid TransferId,
    Guid SourceWalletId,
    Guid DestinationWalletId,
    decimal Amount,
    DateTimeOffset OccurredAtUtc)
    : IIntegrationEvent
{
    public Guid CorrelationId => TransferId;
}
