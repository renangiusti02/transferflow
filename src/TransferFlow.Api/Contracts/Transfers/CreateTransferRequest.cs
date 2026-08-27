namespace TransferFlow.Api.Contracts.Transfers;

public sealed record CreateTransferRequest(
    Guid SourceWalletId,
    Guid DestinationWalletId,
    decimal Amount);