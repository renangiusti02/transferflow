using TransferFlow.Application.Common;

namespace TransferFlow.Integration.Tests.Infrastructure;

internal sealed class CoordinatedUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _inner;
    private readonly AsyncBarrier _barrier;
    private int _hasCoordinated;

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
        if (Interlocked.Exchange(
            ref _hasCoordinated,
            1) == 0)
        {
            await _barrier.SignalAndWaitAsync(
                cancellationToken);
        }

        return await _inner.SaveChangesAsync(
            cancellationToken);
    }
}
