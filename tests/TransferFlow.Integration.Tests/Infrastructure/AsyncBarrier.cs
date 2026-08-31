namespace TransferFlow.Integration.Tests.Infrastructure;

internal sealed class AsyncBarrier
{
    private int _participantsRemaining;

    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AsyncBarrier(int participantCount)
    {
        if (participantCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(participantCount));
        }

        _participantsRemaining =
            participantCount;
    }

    public async Task SignalAndWaitAsync(
        CancellationToken cancellationToken = default)
    {
        var participantsRemaining =
            Interlocked.Decrement(
                ref _participantsRemaining);

        if (participantsRemaining < 0)
        {
            throw new InvalidOperationException(
                "Too many participants reached the barrier.");
        }

        if (participantsRemaining == 0)
        {
            _completion.TrySetResult();
        }

        await _completion.Task.WaitAsync(
            cancellationToken);
    }
}
