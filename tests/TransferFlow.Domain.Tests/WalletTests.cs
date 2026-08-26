using Xunit;
using TransferFlow.Domain;

namespace TransferFlow.Domain.Tests;

public class WalletTests
{
    [Fact]
    public void Wallet_Should_Have_Non_Empty_Id()
    {
        var wallet = new Wallet();

        Assert.NotEqual(Guid.Empty, wallet.Id);
    }

    [Fact]
    public void Wallet_Should_Initialize_With_Zero_Balance()
    {
        var wallet = new Wallet();

        Assert.Equal(0m, wallet.Balance);
    }

    [Fact]
    public void Wallet_Credit_Should_Increase_Balance()
    {
        var wallet = new Wallet();
        var amountToCredit = 100m;

        wallet.Credit(amountToCredit);

        Assert.Equal(amountToCredit, wallet.Balance);
    }

    [Fact]
    public void Wallet_Debit_Should_Decrease_Balance()
    {
        var wallet = new Wallet();
        var amountToCredit = 100m;
        wallet.Credit(amountToCredit);
        var amountToDebit = 50m;

        wallet.Debit(amountToDebit);

        Assert.Equal(amountToCredit - amountToDebit, wallet.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Wallet_Credit_Should_Throw_Exception_For_Non_Positive_Amounts(decimal amountToCredit)
    {
        var wallet = new Wallet();

        Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Credit(amountToCredit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Wallet_Debit_Should_Throw_Exception_For_Non_Positive_Amounts(decimal amountToDebit)
    {
        var wallet = new Wallet();

        Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Debit(amountToDebit));
    }

    [Fact]
    public void Wallet_Debit_Should_Allow_Amount_Equal_To_Balance()
    {
        var wallet = new Wallet();
        wallet.Credit(100m);

        wallet.Debit(100m);

        Assert.Equal(0m, wallet.Balance);
    }

    [Fact]
    public void Wallet_Debit_Should_Throw_Exception_And_Preserve_Balance_When_Insufficient_Funds()
    {
        var wallet = new Wallet();
        var amountToCredit = 100m;
        wallet.Credit(amountToCredit);
        var amountToDebit = 150m;

        Assert.Throws<InvalidOperationException>(() => wallet.Debit(amountToDebit));
        Assert.Equal(amountToCredit, wallet.Balance);
    }

    [Fact]
    public void Wallet_Credit_Should_Throw_Exception_When_Decimal_Precision_Exceeds()
    {
        var wallet = new Wallet();
        var amountToCredit = 100.123m;

        Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Credit(amountToCredit));
    }

    [Fact]
    public void Wallet_Debit_Should_Throw_Exception_When_Decimal_Precision_Exceeds()
    {
        var wallet = new Wallet();
        wallet.Credit(100m);
        var amountToDebit = 50.123m;

        Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Debit(amountToDebit));
    }

    [Fact]
    public void Wallet_Credit_Should_Allow_Maximum_Decimal_Precision()
    {
        var wallet = new Wallet();
        var amountToCredit = 100.12m;

        wallet.Credit(amountToCredit);

        Assert.Equal(amountToCredit, wallet.Balance);
    }

    [Fact]
    public void Wallet_Debit_Should_Allow_Maximum_Decimal_Precision()
    {
        var wallet = new Wallet();
        wallet.Credit(100.12m);
        var amountToDebit = 50.12m;

        wallet.Debit(amountToDebit);

        Assert.Equal(50m, wallet.Balance);
    }
}
