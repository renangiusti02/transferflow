using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TransferFlow.Infrastructure.Messaging.Outbox;

internal sealed class OutboxBackgroundService(IServiceScopeFactory scopeFactory)
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using (var scope =
                _scopeFactory.CreateAsyncScope())
            {
                var processor =
                    scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                await processor.ProcessPendingAsync(stoppingToken);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
