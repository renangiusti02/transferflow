using TransferFlow.Application.Common;

namespace TransferFlow.Integration.Tests.Infrastructure;

internal sealed class CoordinatedUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _inner;
    private readonly AsyncBarrier _barrier;

    public CoordinatedUnitOfWork(
        IUnitOfWork inner,
        AsyncBarrier barrier)
    {
        _inner = inner;
        _barrier = barrier;
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _barrier.SignalAndWaitAsync(cancellationToken);

        return await _inner.SaveChangesAsync(
            cancellationToken);
    }
}
