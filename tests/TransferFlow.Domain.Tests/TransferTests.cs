using Xunit;
using TransferFlow.Domain;

namespace TransferFlow.Domain.Tests;

public class TransferTests
{
    [Fact]
    public void Transfer_Should_Have_Non_Empty_Id()
    {
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100m;
        var idempotencyKey = Guid.NewGuid().ToString();

        var transfer = new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey);

        Assert.NotEqual(Guid.Empty, transfer.Id);
    }

    [Fact]
    public void Transfer_Should_Throw_When_Source_Wallet_Id_Is_Empty()
    {
        var destinationWalletId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();

        Assert.Throws<ArgumentException>(() =>
            new Transfer(Guid.Empty, destinationWalletId, 100m, idempotencyKey));
    }

    [Fact]
    public void Transfer_Should_Throw_When_Destination_Wallet_Id_Is_Empty()
    {
        var sourceWalletId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();

        Assert.Throws<ArgumentException>(() =>
            new Transfer(sourceWalletId, Guid.Empty, 100m, idempotencyKey));
    }

    [Fact]
    public void Transfer_Should_Store_Source_And_Destination_Wallets()
    {
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100m;
        var idempotencyKey = Guid.NewGuid().ToString();

        var transfer = new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey);

        Assert.Equal(sourceWalletId, transfer.SourceWalletId);
        Assert.Equal(destinationWalletId, transfer.DestinationWalletId);
    }

    [Fact]
    public void Transfer_Should_Store_Amount()
    {
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100m;
        var idempotencyKey = Guid.NewGuid().ToString();

        var transfer = new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey);

        Assert.Equal(amount, transfer.Amount);
    }

    [Fact]
    public void Transfer_Should_Store_Creation_Timestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100m;
        var idempotencyKey = Guid.NewGuid().ToString();

        var transfer = new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey);
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(transfer.CreatedAtUtc, before, after);
    }

    [Fact]
    public void Transfer_Should_Throw_Exception_For_Same_Source_And_Destination_Wallets()
    {
        var walletId = Guid.NewGuid();
        var amount = 100m;
        var idempotencyKey = Guid.NewGuid().ToString();

        Assert.Throws<ArgumentException>(() => new Transfer(walletId, walletId, amount, idempotencyKey));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Transfer_Should_Throw_Exception_For_Non_Positive_Amount(decimal amount)
    {
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();

        Assert.Throws<ArgumentOutOfRangeException>(() => new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey));
    }

    [Fact]
    public void Transfer_Should_Throw_Exception_When_Decimal_Precision_Exceeds()
    {
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100.123m;
        var idempotencyKey = Guid.NewGuid().ToString();

        Assert.Throws<ArgumentOutOfRangeException>(() => new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey));
    }

    [Fact]
    public void Transfer_Should_Allow_Maximum_Decimal_Precision()
    {
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100.12m;
        var idempotencyKey = Guid.NewGuid().ToString();

        var transfer = new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey);

        Assert.Equal(amount, transfer.Amount);
    }

    [Fact]
    public void Transfer_Should_Throw_When_Idempotency_Key_Is_Empty()
    {
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100m;
        var idempotencyKey = "";

        Assert.Throws<ArgumentException>(() => new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey));
    }

    [Fact]
    public void Transfer_Should_Throw_When_Idempotency_Key_Exceeds_Maximum_Length()
    { 
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100m;
        var idempotencyKey = new string('a', 101);

        Assert.Throws<ArgumentException>(() => new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey));
    }

    [Fact]
    public void Transfer_Should_Store_Idempotency_Key()
    {
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var amount = 100m;
        var idempotencyKey = new string('a', 100);

        var transfer = new Transfer(sourceWalletId, destinationWalletId, amount, idempotencyKey);

        Assert.Equal(idempotencyKey, transfer.IdempotencyKey);
    }
}
