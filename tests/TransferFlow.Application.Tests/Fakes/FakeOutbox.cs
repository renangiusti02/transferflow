using TransferFlow.Application.Messaging;

namespace TransferFlow.Application.Tests.Fakes;
internal class FakeOutbox : IOutbox
{
    public List<IIntegrationEvent> Messages { get; } = [];
    public void Add<T>(T message)
        where T : IIntegrationEvent
    {
        Messages.Add(message);
    }
}
