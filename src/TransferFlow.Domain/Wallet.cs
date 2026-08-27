namespace TransferFlow.Domain;

public class Wallet
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public decimal Balance { get; private set; }

    public void Credit(decimal amount)
    {
        ValidateAmount(amount);
        Balance += amount;
    }

    public void Debit(decimal amount)
    {
        ValidateAmount(amount);
        if (amount > Balance)
        {
            throw new InvalidOperationException("Insufficient funds.");
        }
        Balance -= amount;
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
