namespace TransferFlow.Application.Messaging;

public interface IOutbox
{
    void Add<T>(T message)
        where T : IIntegrationEvent;
}
