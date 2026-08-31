using TransferFlow.Application.Common;

namespace TransferFlow.Integration.Tests.Infrastructure;

internal sealed class CoordinatedUnitOfWork : IUnitOfWork
{
    private readonly IUnitOfWork _inner;
    private readonly Barrier _barrier;

    public CoordinatedUnitOfWork(
        IUnitOfWork inner,
        Barrier barrier)
    {
        _inner = inner;
        _barrier = barrier;
    }

    public async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        _barrier.SignalAndWait(cancellationToken);

        return await _inner.SaveChangesAsync(
            cancellationToken);
    }
}
