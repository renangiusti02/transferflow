namespace TransferFlow.Application.Wallets;

public sealed record WalletResponse(
    Guid Id,
    decimal Balance);