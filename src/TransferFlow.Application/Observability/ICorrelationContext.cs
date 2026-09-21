namespace TransferFlow.Application.Observability;

public interface ICorrelationContext
{
    Guid CorrelationId { get; }
}
