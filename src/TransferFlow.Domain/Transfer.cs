namespace TransferFlow.Domain;

public class Transfer
{
    public Guid Id { get; private set; }
    public Guid SourceWalletId { get; private set; }

    public Guid DestinationWalletId { get; private set; }

    public decimal Amount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Transfer()
    {
    }

    public Transfer(
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount)
    {
        if (sourceWalletId == Guid.Empty)
        {
            throw new ArgumentException(
                "Source wallet ID cannot be empty.",
                nameof(sourceWalletId));
        }
        if (destinationWalletId == Guid.Empty)
        {
            throw new ArgumentException(
                "Destination wallet ID cannot be empty.",
                nameof(destinationWalletId));
        }
        if (sourceWalletId == destinationWalletId)
        {
            throw new ArgumentException(
                "Source and destination wallets must be different.",
                nameof(destinationWalletId));
        }

        ValidateAmount(amount);

        Id = Guid.NewGuid();
        SourceWalletId = sourceWalletId;
        DestinationWalletId = destinationWalletId;
        Amount = amount;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount must be greater than zero.");
        }

        if (amount != decimal.Round(amount, 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Amount must have at most two decimal places.");
        }
    }
}
