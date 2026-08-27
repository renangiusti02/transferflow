using TransferFlow.Application.Tests.Fakes;
using TransferFlow.Application.Transfers;
using TransferFlow.Domain;
using Xunit;

namespace TransferFlow.Application.Tests.Transfers;

public class CreateTransferUseCaseTests
{
    private readonly FakeWalletRepository _fakeWalletRepository;
    private readonly FakeTransferRepository _fakeTransferRepository;
    private readonly FakeUnitOfWork _fakeUnitOfWork;
    private readonly CreateTransferUseCase _sut;
    private readonly Wallet _sourceWallet;
    private readonly Wallet _destinationWallet;
    public CreateTransferUseCaseTests()
    {
        _fakeWalletRepository = new FakeWalletRepository();
        _fakeTransferRepository = new FakeTransferRepository();
        _fakeUnitOfWork = new FakeUnitOfWork();

        _sourceWallet = new Wallet();
        _sourceWallet.Credit(100m);

        _destinationWallet = new Wallet();
        _destinationWallet.Credit(20m);

        _fakeWalletRepository.Add(_sourceWallet);
        _fakeWalletRepository.Add(_destinationWallet);

        _sut = new CreateTransferUseCase(
            _fakeWalletRepository,
            _fakeTransferRepository,
            _fakeUnitOfWork);
    }
    [Fact]
    public async Task Transfer_Should_Throw_When_Source_Wallet_Does_Not_Exist()
    {
        var invalidSourceWalletId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ExecuteAsync(invalidSourceWalletId, _destinationWallet.Id, 30m)
        );
        Assert.Equal(0, _fakeUnitOfWork.SaveChangesCallCount);
        Assert.Empty(_fakeTransferRepository.Transfers);
    }

    [Fact]
    public async Task Transfer_Should_Throw_When_Destination_Wallet_Does_Not_Exist()
    {
        var invalidDestinationWalletId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ExecuteAsync(_sourceWallet.Id, invalidDestinationWalletId, 30m)
        );
        Assert.Equal(0, _fakeUnitOfWork.SaveChangesCallCount);
        Assert.Empty(_fakeTransferRepository.Transfers);
    }

    [Fact]
    public async Task Transfer_Should_Throw_When_Source_Wallet_Balance_Is_Insufficient()
    {
        var sourceBalanceBefore = _sourceWallet.Balance;
        var destinationBalanceBefore = _destinationWallet.Balance;
        var invalidAmount = 150m;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(_sourceWallet.Id, _destinationWallet.Id, invalidAmount)
        );
        Assert.Equal(sourceBalanceBefore, _sourceWallet.Balance);
        Assert.Equal(destinationBalanceBefore, _destinationWallet.Balance);
        Assert.Equal(0, _fakeUnitOfWork.SaveChangesCallCount);
        Assert.Empty(_fakeTransferRepository.Transfers);
    }

    [Fact]
    public async Task Transfer_Should_Update_Balances_Add_Transfer_And_Save()
    {
        var sourceBalanceBefore = _sourceWallet.Balance;
        var destinationBalanceBefore = _destinationWallet.Balance;
        var amount = 30m;

        var response = await _sut.ExecuteAsync(
            _sourceWallet.Id,
            _destinationWallet.Id,
            amount);

        Assert.Equal(sourceBalanceBefore - 30m, _sourceWallet.Balance);
        Assert.Equal(destinationBalanceBefore + 30m, _destinationWallet.Balance);
        Assert.Equal(1, _fakeUnitOfWork.SaveChangesCallCount);

        var transfer = Assert.Single(_fakeTransferRepository.Transfers);

        Assert.Equal(response.Id, transfer.Id);
        Assert.Equal(_sourceWallet.Id, response.SourceWalletId);
        Assert.Equal(_destinationWallet.Id, response.DestinationWalletId);
        Assert.Equal(amount, response.Amount);
    }
}
