using Microsoft.Extensions.DependencyInjection;

namespace TransferFlow.Infrastructure.Messaging.Outbox;

public static class OutboxDependencyInjection
{
    public static IServiceCollection AddOutboxProcessing(
        this IServiceCollection services)
    {
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();

        return services;
    }
}
