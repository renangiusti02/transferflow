namespace TransferFlow.Application.Messaging;

public interface IIntegrationEvent
{
    DateTimeOffset OccurredAtUtc { get; }
}
