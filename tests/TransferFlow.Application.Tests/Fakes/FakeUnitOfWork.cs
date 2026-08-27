using TransferFlow.Application.Common;

namespace TransferFlow.Application.Tests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;

        return Task.FromResult(1);
    }
}