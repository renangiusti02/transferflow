namespace TransferFlow.Application.Messaging;

public interface IIntegrationEvent
{
    Guid CorrelationId { get; }
    DateTimeOffset OccurredAtUtc { get; }
}
