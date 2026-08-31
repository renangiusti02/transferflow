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
    private readonly string _validIdempotencyKey = new('a', 100);

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
            _sut.ExecuteAsync(invalidSourceWalletId, _destinationWallet.Id, 30m, _validIdempotencyKey)
        );
        Assert.Equal(0, _fakeUnitOfWork.SaveChangesCallCount);
        Assert.Empty(_fakeTransferRepository.Transfers);
    }

    [Fact]
    public async Task Transfer_Should_Throw_When_Destination_Wallet_Does_Not_Exist()
    {
        var invalidDestinationWalletId = Guid.NewGuid();
        var sourceBalanceBefore = _sourceWallet.Balance;

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.ExecuteAsync(_sourceWallet.Id, invalidDestinationWalletId, 30m, _validIdempotencyKey)
        );
        Assert.Equal(0, _fakeUnitOfWork.SaveChangesCallCount);
        Assert.Empty(_fakeTransferRepository.Transfers);
        Assert.Equal(sourceBalanceBefore, _sourceWallet.Balance);
    }

    [Fact]
    public async Task Transfer_Should_Throw_When_Source_Wallet_Balance_Is_Insufficient()
    {
        var sourceBalanceBefore = _sourceWallet.Balance;
        var destinationBalanceBefore = _destinationWallet.Balance;
        var invalidAmount = 150m;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(_sourceWallet.Id, _destinationWallet.Id, invalidAmount, _validIdempotencyKey)
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
            amount,
            _validIdempotencyKey);

        var transfer = Assert.Single(_fakeTransferRepository.Transfers);
        Assert.Equal(sourceBalanceBefore - amount, _sourceWallet.Balance);
        Assert.Equal(destinationBalanceBefore + amount, _destinationWallet.Balance);
        Assert.Equal(1, _fakeUnitOfWork.SaveChangesCallCount);
        Assert.Equal(response.Id, transfer.Id);
        Assert.Equal(_sourceWallet.Id, response.SourceWalletId);
        Assert.Equal(_destinationWallet.Id, response.DestinationWalletId);
        Assert.Equal(amount, response.Amount);
    }

    [Fact]
    public async Task Transfer_Should_Return_Existing_Transfer_On_Idempotent_Retry()
    {
        var sourceBalanceBefore = _sourceWallet.Balance;
        var destinationBalanceBefore = _destinationWallet.Balance;
        var amount = 30m;

        var firstResponse = await _sut.ExecuteAsync(
            _sourceWallet.Id,
            _destinationWallet.Id,
            amount,
            _validIdempotencyKey);
        var secondResponse = await _sut.ExecuteAsync(
            _sourceWallet.Id,
            _destinationWallet.Id,
            amount,
            _validIdempotencyKey);

        var transfer = Assert.Single(_fakeTransferRepository.Transfers);
        Assert.Equal(firstResponse, secondResponse);
        Assert.Equal(sourceBalanceBefore - amount, _sourceWallet.Balance);
        Assert.Equal(destinationBalanceBefore + amount, _destinationWallet.Balance);
        Assert.Equal(1, _fakeUnitOfWork.SaveChangesCallCount);
        Assert.Equal(firstResponse.Id, transfer.Id);
        Assert.Equal(secondResponse.Id, transfer.Id);
    }

    [Fact]
    public async Task Transfer_Should_Reject_Same_Idempotency_Key_With_Different_Payload()
    {
        var sourceBalanceBefore = _sourceWallet.Balance;
        var destinationBalanceBefore = _destinationWallet.Balance;
        var amount = 30m;
        var differentAmount = 50m;

        await _sut.ExecuteAsync(
            _sourceWallet.Id,
            _destinationWallet.Id,
            amount,
            _validIdempotencyKey);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(
                _sourceWallet.Id,
                _destinationWallet.Id,
                differentAmount,
                _validIdempotencyKey)
        );
        Assert.Single(_fakeTransferRepository.Transfers);
        Assert.Equal(sourceBalanceBefore - 30m, _sourceWallet.Balance);
        Assert.Equal(destinationBalanceBefore + 30m, _destinationWallet.Balance);
        Assert.Equal(1, _fakeUnitOfWork.SaveChangesCallCount);
    }
}
