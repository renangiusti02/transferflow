namespace TransferFlow.Application.Common;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(
        Exception innerException)
        : base(
            "The operation conflicted with a concurrent update.",
            innerException)
    {
    }
}
