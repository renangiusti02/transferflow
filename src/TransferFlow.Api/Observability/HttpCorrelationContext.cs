using TransferFlow.Application.Observability;

namespace TransferFlow.Api.Observability;

internal sealed class HttpCorrelationContext(
    IHttpContextAccessor httpContextAccessor)
    : ICorrelationContext
{
    public Guid CorrelationId
    {
        get
        {
            var value =
                httpContextAccessor
                    .HttpContext?
                    .TraceIdentifier;

            if (!Guid.TryParse(value, out var correlationId))
            {
                throw new InvalidOperationException(
                    "A valid correlation ID is not available.");
            }

            return correlationId;
        }
    }
}
