namespace TransferFlow.Application.Transfers;

public sealed record TransferResponse(
    Guid Id,
    Guid SourceWalletId,
    Guid DestinationWalletId,
    decimal Amount,
    DateTimeOffset CreatedAtUtc);