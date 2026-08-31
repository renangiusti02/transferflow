namespace TransferFlow.Application.Common;

public sealed class DuplicateIdempotencyKeyException : Exception
{
    public DuplicateIdempotencyKeyException(Exception innerException)
        : base(
            "The idempotency key is already in use.",
            innerException)
    {
    }
}